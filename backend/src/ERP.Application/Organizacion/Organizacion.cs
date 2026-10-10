using ERP.Application.Auditoria;
using ERP.Application.Comun;
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
    public bool Activo { get; init; }
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

public sealed class EmpresasService(IEmpresaRepository repositorio, IAccesosUsuarioActual accesos)
{
    /// <summary>Solo las empresas a las que el usuario tiene acceso.</summary>
    public async Task<IReadOnlyList<EmpresaDto>> ListarAsync(CancellationToken ct) =>
        await repositorio.ListarAsync(await accesos.EmpresasPermitidasAsync(ct), ct);
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
