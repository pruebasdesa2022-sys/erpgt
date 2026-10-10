using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Catalogos;

// Motor de los catálogos "código + nombre + datos propios" (bodegas, marcas, monedas, ...).
// Cada catálogo define sus DTO, sus validadores extra y, si hace falta, un IReglasCatalogo; el
// repositorio (Infraestructura) declara su tabla. Todo lo demás —unicidad por empresa, concurrencia
// optimista, baja lógica, auditoría en la misma transacción— vive aquí una sola vez.

/// <summary>Dónde viven las filas de un catálogo.</summary>
public enum AlcanceCatalogo
{
    /// <summary>Todas las filas pertenecen a una empresa.</summary>
    Empresa,

    /// <summary>Catálogo compartido por todas las empresas (sin empresa_id).</summary>
    Global,

    /// <summary>Estándar del sistema (empresa_id nulo, solo lectura) más las propias de la empresa.</summary>
    EmpresaOGlobal,
}

/// <param name="Modulo">Módulo dueño, para la bitácora.</param>
/// <param name="Entidad">Nombre de la entidad en la bitácora (la tabla).</param>
/// <param name="Singular">Para los mensajes: "bodega", "unidad de medida".</param>
/// <param name="Femenino">Concordancia de los mensajes ("la bodega" / "el banco").</param>
public sealed record CatalogoInfo(string Modulo, string Entidad, string Singular, bool Femenino, AlcanceCatalogo Alcance = AlcanceCatalogo.Empresa)
{
    public string NoExiste => $"{(Femenino ? "La" : "El")} {Singular} no existe.";

    public string CodigoDuplicado(string codigo) =>
        $"Ya existe {(Femenino ? "una" : "un")} {Singular} con el código {codigo}.";
}

public abstract record CatalogoDto
{
    public long Id { get; init; }

    /// <summary>Nulo en los catálogos globales y en las unidades estándar.</summary>
    public long? EmpresaId { get; init; }

    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }
}

public abstract record CrearCatalogo
{
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public abstract record EditarCatalogo
{
    public string Nombre { get; init; } = "";
    public bool Activo { get; init; } = true;
    public uint VersionFila { get; init; }
}

public sealed record ConsultaCatalogo : ConsultaPaginada
{
    public bool? Activo { get; init; }
}

public class CrearCatalogoValidator<T> : AbstractValidator<T> where T : CrearCatalogo
{
    public CrearCatalogoValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(20)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo admite letras, números, guion y guion bajo.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
    }
}

public class EditarCatalogoValidator<T> : AbstractValidator<T> where T : EditarCatalogo
{
    public EditarCatalogoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

// ----- Puertos -----

public interface ICatalogoRepository<TDto, TCrear, TEditar>
    where TDto : CatalogoDto
    where TCrear : CrearCatalogo
    where TEditar : EditarCatalogo
{
    CatalogoInfo Info { get; }

    Task<ResultadoPaginado<TDto>> ListarAsync(long empresaId, ConsultaCatalogo consulta, CancellationToken ct);

    Task<IReadOnlyList<TDto>> ListarParaExportarAsync(long empresaId, ConsultaCatalogo consulta, int maximo, CancellationToken ct);

    /// <summary>Dentro de una transacción bloquea la fila.</summary>
    Task<TDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    Task<bool> ExisteCodigoAsync(long empresaId, string codigo, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, string codigo, TCrear dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Devuelve false si <c>version_fila</c> ya no coincide (edición concurrente).</summary>
    Task<bool> ActualizarAsync(long empresaId, long id, TEditar dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Baja lógica; false si <c>version_fila</c> ya no coincide.</summary>
    Task<bool> DesactivarAsync(long empresaId, long id, uint versionFila, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

/// <summary>Reglas propias de un catálogo (referencias, dependientes, coherencias). Se ejecutan dentro de la transacción.</summary>
public interface IReglasCatalogo<TDto, TCrear, TEditar>
    where TDto : CatalogoDto
    where TCrear : CrearCatalogo
    where TEditar : EditarCatalogo
{
    Task ValidarCrearAsync(long empresaId, TCrear dto, CancellationToken ct);

    Task ValidarEditarAsync(long empresaId, TDto anterior, TEditar dto, CancellationToken ct);

    /// <summary>Se invoca al desactivar, ya sea con la baja o editando <c>Activo = false</c>.</summary>
    Task ValidarDesactivarAsync(long empresaId, TDto anterior, CancellationToken ct);
}

public sealed class SinReglasCatalogo<TDto, TCrear, TEditar> : IReglasCatalogo<TDto, TCrear, TEditar>
    where TDto : CatalogoDto
    where TCrear : CrearCatalogo
    where TEditar : EditarCatalogo
{
    public Task ValidarCrearAsync(long empresaId, TCrear dto, CancellationToken ct) => Task.CompletedTask;

    public Task ValidarEditarAsync(long empresaId, TDto anterior, TEditar dto, CancellationToken ct) => Task.CompletedTask;

    public Task ValidarDesactivarAsync(long empresaId, TDto anterior, CancellationToken ct) => Task.CompletedTask;
}

public enum EntidadReferencia
{
    Sucursal,
    Bodega,
    Caja,
    Departamento,
    Banco,
    Moneda,
    TipoDocumento,
}

public enum RelacionDependiente
{
    BodegasDeSucursal,
    CajasDeSucursal,
    CajasDeBodega,
    CentrosCostoDeDepartamento,
    CuentasDeBanco,
    CuentasDeMoneda,
    EmpresasDeMonedaLocal,
}

/// <summary>Verificaciones de integridad entre catálogos que las reglas necesitan.</summary>
public interface IReferencias
{
    /// <summary>La fila existe, está activa y pertenece a la empresa (o es global).</summary>
    Task<bool> ExisteActivaAsync(EntidadReferencia entidad, long id, long empresaId, CancellationToken ct);

    /// <summary>Sucursal a la que pertenece una bodega o caja de la empresa; nulo si no existe.</summary>
    Task<long?> SucursalDeAsync(EntidadReferencia entidad, long id, long empresaId, CancellationToken ct);

    Task<long> ContarDependientesActivosAsync(RelacionDependiente relacion, long padreId, CancellationToken ct);
}

// ----- Caso de uso -----

public sealed class CatalogoService<TDto, TCrear, TEditar>(
    ICatalogoRepository<TDto, TCrear, TEditar> repositorio,
    IReglasCatalogo<TDto, TCrear, TEditar> reglas,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
    where TDto : CatalogoDto
    where TCrear : CrearCatalogo
    where TEditar : EditarCatalogo
{
    public const int MaximoExportacion = 10_000;

    private CatalogoInfo Info => repositorio.Info;

    public Task<ResultadoPaginado<TDto>> ListarAsync(ConsultaCatalogo consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<TDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct) ?? throw new NoEncontradoException(Info.NoExiste);

    public Task<TDto> CrearAsync(TCrear dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var codigo = dto.Codigo.Trim().ToUpperInvariant();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (await repositorio.ExisteCodigoAsync(empresaId, codigo, ct))
                throw new ConflictoException("DUPLICADO", Info.CodigoDuplicado(codigo));

            await reglas.ValidarCrearAsync(empresaId, dto, ct);

            var id = await repositorio.InsertarAsync(empresaId, codigo, dto, usuarioId, reloj.UtcNow, ct);
            var creado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Info.Modulo, Info.Entidad, id.ToString(), "CREAR", null, creado), ct);
            return creado;
        }, ct);
    }

    public Task<TDto> EditarAsync(long id, TEditar dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await ObtenerEditableAsync(empresaId, id, ct);

            await reglas.ValidarEditarAsync(empresaId, anterior, dto, ct);
            if (anterior.Activo && !dto.Activo)
                await reglas.ValidarDesactivarAsync(empresaId, anterior, ct);

            if (!await repositorio.ActualizarAsync(empresaId, id, dto, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            var accion = anterior.Activo && !actualizado.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Info.Modulo, Info.Entidad, id.ToString(), accion, anterior, actualizado), ct);
            return actualizado;
        }, ct);
    }

    /// <summary>Baja lógica: queda inactivo y su código sigue reservado.</summary>
    public Task DesactivarAsync(long id, uint versionFila, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await ObtenerEditableAsync(empresaId, id, ct);
            if (!anterior.Activo)
                return;

            await reglas.ValidarDesactivarAsync(empresaId, anterior, ct);

            if (!await repositorio.DesactivarAsync(empresaId, id, versionFila, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Info.Modulo, Info.Entidad, id.ToString(), "ELIMINAR", anterior, actualizado), ct);
        }, ct);
    }

    public async Task<IReadOnlyList<TDto>> ExportarAsync(ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await repositorio.ListarParaExportarAsync(actual.EmpresaIdRequerida(), consulta, MaximoExportacion, ct);
        await auditoria.RegistrarAsync(
            new RegistroAuditoria(Info.Modulo, Info.Entidad, null, "EXPORTAR", Mensaje: $"{filas.Count} filas exportadas"), ct);
        return filas;
    }

    private async Task<TDto> ObtenerEditableAsync(long empresaId, long id, CancellationToken ct)
    {
        var fila = await repositorio.ObtenerAsync(empresaId, id, ct) ?? throw new NoEncontradoException(Info.NoExiste);

        if (Info.Alcance == AlcanceCatalogo.EmpresaOGlobal && fila.EmpresaId is null)
            throw new ReglaNegocioException("ESTANDAR_SOLO_LECTURA",
                $"{(Info.Femenino ? "La" : "El")} {Info.Singular} es estándar del sistema y no se puede modificar.");

        return fila;
    }
}
