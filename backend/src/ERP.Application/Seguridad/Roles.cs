using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Seguridad;

// ----- DTO -----

public sealed record RolListaDto
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string? Descripcion { get; init; }
    public bool EsSistema { get; init; }
    public bool Activo { get; init; }
    public long CantidadPermisos { get; init; }
    public long CantidadUsuarios { get; init; }
    public uint VersionFila { get; init; }
}

public sealed record RolDetalleDto
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string? Descripcion { get; init; }
    public bool EsSistema { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public IReadOnlyList<long> PermisoIds { get; init; } = [];
}

public sealed record CrearRolDto(string Codigo, string Nombre, string? Descripcion, IReadOnlyList<long> PermisoIds);

public sealed record EditarRolDto(string Nombre, string? Descripcion, bool Activo, IReadOnlyList<long> PermisoIds, uint VersionFila);

public sealed record PermisoDto
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Modulo { get; init; } = "";
    public string ModuloNombre { get; init; } = "";
    public string Recurso { get; init; } = "";
    public string Accion { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public sealed record ConsultaRoles : ConsultaPaginada
{
    public bool? Activo { get; init; }
}

// ----- Validadores -----

public sealed class CrearRolValidator : AbstractValidator<CrearRolDto>
{
    public CrearRolValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9_]+$").WithMessage("El código solo admite letras, números y guion bajo.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.PermisoIds).NotNull();
    }
}

public sealed class EditarRolValidator : AbstractValidator<EditarRolDto>
{
    public EditarRolValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.PermisoIds).NotNull();
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

// ----- Puerto -----

public interface IRolRepository
{
    Task<ResultadoPaginado<RolListaDto>> ListarAsync(ConsultaRoles consulta, CancellationToken ct);

    Task<RolDetalleDto?> ObtenerAsync(long id, CancellationToken ct);

    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct);

    Task<long> InsertarAsync(string codigo, string nombre, string? descripcion, long creadoPor, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long id, string nombre, string? descripcion, bool activo, uint versionFila, long modificadoPor, DateTime ahoraUtc, CancellationToken ct);

    Task ReemplazarPermisosAsync(long id, IReadOnlyCollection<long> permisoIds, long por, CancellationToken ct);

    Task<int> ContarUsuariosActivosAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct);
}

// ----- Caso de uso -----

public sealed class RolesService(
    IRolRepository repositorio,
    IAutorizacionRepository autorizacion,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    private const string Modulo = "SEGURIDAD";
    private const string Entidad = "rol";

    public Task<ResultadoPaginado<RolListaDto>> ListarAsync(ConsultaRoles consulta, CancellationToken ct) =>
        repositorio.ListarAsync(consulta, ct);

    public async Task<RolDetalleDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El rol no existe.");

    public Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct) => repositorio.ListarPermisosAsync(ct);

    public Task<RolDetalleDto> CrearAsync(CrearRolDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();
        var codigo = dto.Codigo.Trim().ToUpperInvariant();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (await repositorio.ExisteCodigoAsync(codigo, ct))
                throw new ConflictoException("DUPLICADO", $"Ya existe un rol con el código {codigo}.");

            var permisos = await ValidarPermisosAsync(dto.PermisoIds, ct);
            var id = await repositorio.InsertarAsync(codigo, dto.Nombre.Trim(), Limpiar(dto.Descripcion), por, reloj.UtcNow, ct);
            await repositorio.ReemplazarPermisosAsync(id, permisos, por, ct);
            await autorizacion.IncrementarVersionAsync(ct);

            var creado = (await repositorio.ObtenerAsync(id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creado), ct);
            return creado;
        }, ct);
    }

    public Task<RolDetalleDto> EditarAsync(long id, EditarRolDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El rol no existe.");

            // El rol de sistema recibe todos los permisos por script y es la garantía de que
            // siempre haya quien administre: no se edita ni se desactiva desde la aplicación.
            if (anterior.EsSistema)
                throw new ReglaNegocioException("ROL_SISTEMA", "El rol de sistema no se puede modificar.");

            if (anterior.Activo && !dto.Activo && await repositorio.ContarUsuariosActivosAsync(id, ct) > 0)
                throw new ReglaNegocioException("ROL_EN_USO", "El rol está asignado a usuarios activos. Quíteselo antes de desactivarlo.");

            var permisos = await ValidarPermisosAsync(dto.PermisoIds, ct);

            if (!await repositorio.ActualizarAsync(id, dto.Nombre.Trim(), Limpiar(dto.Descripcion), dto.Activo, dto.VersionFila, por, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            await repositorio.ReemplazarPermisosAsync(id, permisos, por, ct);
            await autorizacion.IncrementarVersionAsync(ct);

            var actualizado = (await repositorio.ObtenerAsync(id, ct))!;
            var accion = anterior.Activo && !actualizado.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizado), ct);
            return actualizado;
        }, ct);
    }

    public async Task DesactivarAsync(long id, uint versionFila, CancellationToken ct)
    {
        var existente = await ObtenerAsync(id, ct);
        await EditarAsync(id, new EditarRolDto(existente.Nombre, existente.Descripcion, false, existente.PermisoIds, versionFila), ct);
    }

    private async Task<IReadOnlyCollection<long>> ValidarPermisosAsync(IReadOnlyList<long> permisoIds, CancellationToken ct)
    {
        var ids = permisoIds.Distinct().ToList();
        var existentes = (await repositorio.ListarPermisosAsync(ct)).Select(p => p.Id).ToHashSet();
        if (ids.Any(i => !existentes.Contains(i)))
            throw new ValidacionException("permisoIds", "Uno o más permisos no existen.");
        return ids;
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
