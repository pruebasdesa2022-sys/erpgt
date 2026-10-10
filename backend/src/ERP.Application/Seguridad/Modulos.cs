using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using ERP.Domain.Seguridad;
using FluentValidation;

namespace ERP.Application.Seguridad;

// ----- Módulos -----

/// <summary>Fila de módulo con su estado global y el de la empresa activa, tal como están guardados.</summary>
public sealed record ModuloFila
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public int Fase { get; init; }
    public int Orden { get; init; }
    public string EstadoGlobal { get; init; } = "";
    public string? EstadoEmpresa { get; init; }
    public bool EsBase { get; init; }
    public uint VersionFila { get; init; }
    public uint? VersionFilaEmpresa { get; init; }
}

public sealed record ModuloDto(long Id, string Codigo, string Nombre, int Fase, int Orden, string EstadoGlobal,
    string? EstadoEmpresa, string EstadoEfectivo, bool EsBase, uint VersionFila, uint? VersionFilaEmpresa);

/// <param name="Ambito">GLOBAL cambia el estado del ambiente; EMPRESA, el de la empresa activa.</param>
/// <param name="VersionFila">Versión de la fila que se modifica; 0 si la empresa aún no tiene estado propio.</param>
public sealed record CambiarEstadoModuloDto(string Ambito, string Estado, string Motivo, uint VersionFila);

public sealed class CambiarEstadoModuloValidator : AbstractValidator<CambiarEstadoModuloDto>
{
    public CambiarEstadoModuloValidator()
    {
        RuleFor(x => x.Ambito).Must(a => a is "GLOBAL" or "EMPRESA").WithMessage("El ámbito debe ser GLOBAL o EMPRESA.");
        RuleFor(x => x.Estado).IsEnumName(typeof(EstadoModulo)).WithMessage("El estado no es válido.");
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Indique el motivo del cambio.").MaximumLength(500);
    }
}

public interface IModuloRepository
{
    Task<IReadOnlyList<ModuloFila>> ListarAsync(long empresaId, CancellationToken ct);

    Task<ModuloFila?> ObtenerAsync(long id, long empresaId, CancellationToken ct);

    Task<bool> ActualizarGlobalAsync(long id, string estado, uint versionFila, long por, DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Crea la fila de la empresa si no existe (versionFila 0) o la actualiza si la versión coincide.</summary>
    Task<bool> GuardarEstadoEmpresaAsync(long id, long empresaId, string estado, string motivo, uint versionFila, long por, DateTime ahoraUtc, CancellationToken ct);
}

public sealed class ModulosService(
    IModuloRepository repositorio,
    IAutorizacionRepository autorizacion,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    public async Task<IReadOnlyList<ModuloDto>> ListarAsync(CancellationToken ct) =>
        (await repositorio.ListarAsync(actual.EmpresaIdRequerida(), ct)).Select(ADto).ToList();

    public Task<ModuloDto> CambiarEstadoAsync(long id, CambiarEstadoModuloDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var por = actual.UsuarioIdRequerido();
        var nuevo = Enum.Parse<EstadoModulo>(dto.Estado);
        var motivo = dto.Motivo.Trim();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(id, empresaId, ct) ?? throw new NoEncontradoException("El módulo no existe.");

            if (!ReglasModulo.CambioPermitido(anterior.EsBase, nuevo))
                throw new ReglaNegocioException("MODULO_BASE", $"El módulo {anterior.Nombre} es parte de la base del sistema y debe permanecer liberado.");

            var guardado = dto.Ambito == "GLOBAL"
                ? await repositorio.ActualizarGlobalAsync(id, dto.Estado, dto.VersionFila, por, reloj.UtcNow, ct)
                : await repositorio.GuardarEstadoEmpresaAsync(id, empresaId, dto.Estado, motivo, dto.VersionFila, por, reloj.UtcNow, ct);
            if (!guardado)
                throw ConflictoException.Concurrencia();

            await autorizacion.IncrementarVersionAsync(ct);

            var actualizado = ADto((await repositorio.ObtenerAsync(id, empresaId, ct))!);
            await auditoria.RegistrarAsync(new RegistroAuditoria("SEGURIDAD", "modulo", id.ToString(), "CAMBIAR_ESTADO",
                ADto(anterior), actualizado, $"Ámbito {dto.Ambito}. Motivo: {motivo}"), ct);
            return actualizado;
        }, ct);
    }

    private static ModuloDto ADto(ModuloFila fila)
    {
        var global = Enum.Parse<EstadoModulo>(fila.EstadoGlobal);
        EstadoModulo? empresa = fila.EstadoEmpresa is null ? null : Enum.Parse<EstadoModulo>(fila.EstadoEmpresa);
        var efectivo = ReglasModulo.EstadoEfectivo(global, empresa);
        return new ModuloDto(fila.Id, fila.Codigo, fila.Nombre, fila.Fase, fila.Orden, fila.EstadoGlobal, fila.EstadoEmpresa,
            efectivo.ToString(), fila.EsBase, fila.VersionFila, fila.VersionFilaEmpresa);
    }
}

// ----- Menú -----

public sealed record MenuFila
{
    public long Id { get; init; }
    public long? PadreId { get; init; }
    public string Tipo { get; init; } = "";
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string? Icono { get; init; }
    public string? Ruta { get; init; }
    public int Orden { get; init; }
    public string? PermisoCodigo { get; init; }
}

public sealed record MenuItemDto(long Id, string Tipo, string Codigo, string Nombre, string? Icono, string? Ruta, IReadOnlyList<MenuItemDto> Hijos);

public interface IMenuRepository
{
    /// <summary>Elementos activos del menú, sin filtrar por usuario.</summary>
    Task<IReadOnlyList<MenuFila>> ListarActivosAsync(CancellationToken ct);
}

public sealed class MenuService(IMenuRepository repositorio, AutorizacionService autorizacion)
{
    /// <summary>
    /// Árbol filtrado para el usuario: una pantalla aparece solo si puede ejercer su permiso
    /// (lo que ya incluye que el módulo esté accesible); un grupo, solo si conserva algún hijo.
    /// </summary>
    public async Task<IReadOnlyList<MenuItemDto>> ObtenerAsync(CancellationToken ct)
    {
        var permisos = (await autorizacion.PermisosEjerciblesAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var filas = await repositorio.ListarActivosAsync(ct);
        var porPadre = filas.ToLookup(f => f.PadreId);

        return Construir(null);

        IReadOnlyList<MenuItemDto> Construir(long? padreId) =>
            porPadre[padreId]
                .OrderBy(f => f.Orden).ThenBy(f => f.Nombre, StringComparer.CurrentCulture)
                .Select(f => (Fila: f, Hijos: Construir(f.Id)))
                .Where(x => x.Fila.Tipo == "PANTALLA"
                    ? x.Fila.PermisoCodigo is not null && permisos.Contains(x.Fila.PermisoCodigo)
                    : x.Hijos.Count > 0)
                .Select(x => new MenuItemDto(x.Fila.Id, x.Fila.Tipo, x.Fila.Codigo, x.Fila.Nombre, x.Fila.Icono, x.Fila.Ruta, x.Hijos))
                .ToList();
    }
}
