using ERP.Application.Auditoria;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Domain.Catalogos;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Organizacion;

// ----- Contrato hacia otros módulos -----

// Los DTO que Dapper materializa usan propiedades (no constructor posicional): así convierte
// BIGINT UNSIGNED a long sin exigir tipos idénticos.
public sealed record EmpresaResumen
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string NombreComercial { get; init; } = "";
}

public sealed record SucursalResumen
{
    public long Id { get; init; }
    public long EmpresaId { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
}

/// <summary>Lecturas de Organización que otros módulos pueden usar (solo registros activos).</summary>
public interface IOrganizacionConsulta
{
    Task<IReadOnlyList<EmpresaResumen>> ListarEmpresasAsync(CancellationToken ct);

    Task<IReadOnlyList<SucursalResumen>> ListarSucursalesAsync(CancellationToken ct);
}

// ----- DTO -----

public sealed record EmpresaDto
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string RazonSocial { get; init; } = "";
    public string NombreComercial { get; init; } = "";
    public string Nit { get; init; } = "";
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public string RegimenTributario { get; init; } = "GENERAL";
    public long MonedaLocalId { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
}

public static class RegimenesTributarios
{
    public static readonly IReadOnlyList<string> Todos = ["GENERAL", "PEQUENO_CONTRIBUYENTE"];
}

/// <summary>El código y el estado de la empresa no se editan desde la aplicación.</summary>
public sealed record EditarEmpresaDto(
    string RazonSocial,
    string NombreComercial,
    string Nit,
    string? Direccion,
    string? Telefono,
    string? Correo,
    string RegimenTributario,
    long MonedaLocalId,
    uint VersionFila);

public sealed class EditarEmpresaValidator : AbstractValidator<EditarEmpresaDto>
{
    public EditarEmpresaValidator()
    {
        RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NombreComercial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Nit).NotEmpty().MaximumLength(20)
            .Must(Nit.EsValido).WithMessage("El NIT no es válido. Verifique el número y su dígito verificador.");
        RuleFor(x => x.Direccion).MaximumLength(300);
        RuleFor(x => x.Telefono).MaximumLength(30);
        RuleFor(x => x.Correo).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo));
        RuleFor(x => x.RegimenTributario).Must(r => RegimenesTributarios.Todos.Contains(r)).WithMessage("Régimen tributario no válido.");
        RuleFor(x => x.MonedaLocalId).GreaterThan(0).WithMessage("Seleccione la moneda local.");
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

public sealed record SucursalDto
{
    public long Id { get; init; }
    public long EmpresaId { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public bool EsPrincipal { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }
}

public sealed record CrearSucursalDto(string Codigo, string Nombre, string? Direccion, string? Telefono, string? Correo);

public sealed record EditarSucursalDto(string Nombre, string? Direccion, string? Telefono, string? Correo, bool Activo, uint VersionFila);

public sealed record ConsultaSucursales : ConsultaPaginada
{
    public bool? Activo { get; init; }
}

// ----- Validadores -----

public sealed class CrearSucursalValidator : AbstractValidator<CrearSucursalDto>
{
    public CrearSucursalValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(20)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo admite letras, números, guion y guion bajo.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Direccion).MaximumLength(300);
        RuleFor(x => x.Telefono).MaximumLength(30);
        RuleFor(x => x.Correo).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo));
    }
}

public sealed class EditarSucursalValidator : AbstractValidator<EditarSucursalDto>
{
    public EditarSucursalValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Direccion).MaximumLength(300);
        RuleFor(x => x.Telefono).MaximumLength(30);
        RuleFor(x => x.Correo).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo));
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

// ----- Puertos -----

public interface IEmpresaRepository
{
    Task<IReadOnlyList<EmpresaDto>> ListarAsync(IReadOnlyCollection<long> ids, CancellationToken ct);

    Task<EmpresaDto?> ObtenerAsync(long id, CancellationToken ct);

    /// <summary>Devuelve false si <c>version_fila</c> ya no coincide (edición concurrente).</summary>
    Task<bool> ActualizarAsync(long id, EditarEmpresaDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

public interface ISucursalRepository
{
    Task<ResultadoPaginado<SucursalDto>> ListarAsync(long empresaId, ConsultaSucursales consulta, CancellationToken ct);

    Task<IReadOnlyList<SucursalDto>> ListarParaExportarAsync(long empresaId, ConsultaSucursales consulta, int maximo, CancellationToken ct);

    Task<SucursalDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    Task<bool> ExisteCodigoAsync(long empresaId, string codigo, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, CrearSucursalDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Devuelve false si <c>version_fila</c> ya no coincide (edición concurrente).</summary>
    Task<bool> ActualizarAsync(long empresaId, long id, EditarSucursalDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

// ----- Casos de uso -----

public sealed class EmpresasService(
    IEmpresaRepository repositorio,
    IAccesosUsuarioActual accesos,
    IReferencias referencias,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    /// <summary>Solo las empresas a las que el usuario tiene acceso.</summary>
    public async Task<IReadOnlyList<EmpresaDto>> ListarAsync(CancellationToken ct) =>
        await repositorio.ListarAsync(await accesos.EmpresasPermitidasAsync(ct), ct);

    public Task<EmpresaDto> EditarAsync(long id, EditarEmpresaDto dto, CancellationToken ct)
    {
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with
        {
            RazonSocial = dto.RazonSocial.Trim(),
            NombreComercial = dto.NombreComercial.Trim(),
            Nit = Nit.Normalizar(dto.Nit)!,
            Direccion = Limpiar(dto.Direccion),
            Telefono = Limpiar(dto.Telefono),
            Correo = Limpiar(dto.Correo),
        };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            // Una empresa a la que el usuario no tiene acceso no existe para él.
            if (!(await accesos.EmpresasPermitidasAsync(ct)).Contains(id))
                throw new NoEncontradoException("La empresa no existe.");

            // El permiso se resolvió para la empresa activa: solo esa empresa se edita con él.
            if (id != actual.EmpresaIdRequerida())
                throw new AccesoDenegadoException("EMPRESA_NO_ACTIVA", "Cambie a esa empresa para editar sus datos.");

            var anterior = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("La empresa no existe.");

            if (!await referencias.ExisteActivaAsync(EntidadReferencia.Moneda, limpio.MonedaLocalId, id, ct))
                throw new ValidacionException("monedaLocalId", "La moneda no existe o está inactiva.");

            if (!await repositorio.ActualizarAsync(id, limpio, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizada = (await repositorio.ObtenerAsync(id, ct))!;
            await auditoria.RegistrarAsync(
                new RegistroAuditoria("ORGANIZACION", "empresa", id.ToString(), "EDITAR", anterior, actualizada, EmpresaId: id), ct);
            return actualizada;
        }, ct);
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Empresas que el usuario actual puede ver. Lo implementa Seguridad.</summary>
public interface IAccesosUsuarioActual
{
    Task<IReadOnlyCollection<long>> EmpresasPermitidasAsync(CancellationToken ct);
}

/// <summary>
/// Catálogo de referencia de Fase 0: fija el patrón CRUD (filtro por empresa activa, validación,
/// unicidad, concurrencia optimista, baja lógica y auditoría en la misma transacción).
/// </summary>
public sealed class SucursalesService(
    ISucursalRepository repositorio,
    IReferencias referencias,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    public const int MaximoExportacion = 10_000;
    private const string Modulo = "ORGANIZACION";
    private const string Entidad = "sucursal";

    public Task<ResultadoPaginado<SucursalDto>> ListarAsync(ConsultaSucursales consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<SucursalDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
        ?? throw new NoEncontradoException("La sucursal no existe.");

    public Task<SucursalDto> CrearAsync(CrearSucursalDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with
        {
            Codigo = dto.Codigo.Trim().ToUpperInvariant(),
            Nombre = dto.Nombre.Trim(),
            Direccion = Limpiar(dto.Direccion),
            Telefono = Limpiar(dto.Telefono),
            Correo = Limpiar(dto.Correo),
        };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (await repositorio.ExisteCodigoAsync(empresaId, limpio.Codigo, ct))
                throw new ConflictoException("DUPLICADO", $"Ya existe una sucursal con el código {limpio.Codigo}.");

            var id = await repositorio.InsertarAsync(empresaId, limpio, usuarioId, reloj.UtcNow, ct);
            var creada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creada), ct);
            return creada;
        }, ct);
    }

    public Task<SucursalDto> EditarAsync(long id, EditarSucursalDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with
        {
            Nombre = dto.Nombre.Trim(),
            Direccion = Limpiar(dto.Direccion),
            Telefono = Limpiar(dto.Telefono),
            Correo = Limpiar(dto.Correo),
        };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(empresaId, id, ct)
                ?? throw new NoEncontradoException("La sucursal no existe.");

            if (anterior.EsPrincipal && !limpio.Activo)
                throw new ReglaNegocioException("SUCURSAL_PRINCIPAL", "La sucursal principal no se puede desactivar.");

            if (anterior.Activo && !limpio.Activo
                && (await referencias.ContarDependientesActivosAsync(RelacionDependiente.BodegasDeSucursal, id, ct)
                    + await referencias.ContarDependientesActivosAsync(RelacionDependiente.CajasDeSucursal, id, ct)
                    + await referencias.ContarDependientesActivosAsync(RelacionDependiente.SeriesDeSucursal, id, ct)) > 0)
                throw new ReglaNegocioException("TIENE_DEPENDIENTES",
                    "La sucursal tiene bodegas, cajas o series de documentos activas. Desactívelas primero.");

            if (!await repositorio.ActualizarAsync(empresaId, id, limpio, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            var accion = anterior.Activo && !actualizada.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizada), ct);
            return actualizada;
        }, ct);
    }

    /// <summary>Baja lógica: la sucursal queda inactiva y su código sigue reservado.</summary>
    public async Task DesactivarAsync(long id, uint versionFila, CancellationToken ct)
    {
        var existente = await ObtenerAsync(id, ct);
        var dto = new EditarSucursalDto(existente.Nombre, existente.Direccion, existente.Telefono, existente.Correo, false, versionFila);
        await EditarAsync(id, dto, ct);
    }

    public async Task<IReadOnlyList<SucursalDto>> ExportarAsync(ConsultaSucursales consulta, CancellationToken ct)
    {
        var filas = await repositorio.ListarParaExportarAsync(actual.EmpresaIdRequerida(), consulta, MaximoExportacion, ct);
        await auditoria.RegistrarAsync(
            new RegistroAuditoria(Modulo, Entidad, null, "EXPORTAR", Mensaje: $"{filas.Count} filas exportadas"), ct);
        return filas;
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
