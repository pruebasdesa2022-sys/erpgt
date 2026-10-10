using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Domain.Comun;
using ERP.Domain.Seguridad;
using FluentValidation;

namespace ERP.Application.Seguridad;

// ----- DTO -----

public sealed record UsuarioListaDto
{
    public long Id { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string Correo { get; init; } = "";
    public string NombreCompleto { get; init; } = "";
    public bool Activo { get; init; }
    public bool DebeCambiarPassword { get; init; }
    public DateTime? BloqueadoHasta { get; init; }
    public DateTime? UltimoAccesoEn { get; init; }
    public string? Roles { get; init; }
    public uint VersionFila { get; init; }
}

public sealed record UsuarioDetalleDto
{
    public long Id { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string Correo { get; init; } = "";
    public string NombreCompleto { get; init; } = "";
    public bool Activo { get; init; }
    public bool DebeCambiarPassword { get; init; }
    public DateTime? BloqueadoHasta { get; init; }
    public DateTime? UltimoAccesoEn { get; init; }
    public uint VersionFila { get; init; }
    public IReadOnlyList<long> RolIds { get; init; } = [];
    public IReadOnlyList<long> EmpresaIds { get; init; } = [];
    public IReadOnlyList<long> SucursalIds { get; init; } = [];
}

public sealed record CrearUsuarioDto(string NombreUsuario, string Correo, string NombreCompleto, string PasswordTemporal,
    IReadOnlyList<long> RolIds, IReadOnlyList<long> EmpresaIds, IReadOnlyList<long> SucursalIds);

public sealed record EditarUsuarioDto(string Correo, string NombreCompleto, bool Activo,
    IReadOnlyList<long> RolIds, IReadOnlyList<long> EmpresaIds, IReadOnlyList<long> SucursalIds, uint VersionFila);

public sealed record RestablecerPasswordAdminDto(string PasswordTemporal);

public sealed record PermisoDirectoDto
{
    public long PermisoId { get; init; }
    public string Codigo { get; init; } = "";
    public string Efecto { get; init; } = "";
    public DateTime? VigenteHasta { get; init; }
    public string? Motivo { get; init; }
}

public sealed record PermisoDirectoEntradaDto(long PermisoId, string Efecto, DateTime? VigenteHasta, string? Motivo);

public sealed record GuardarPermisosDirectosDto(IReadOnlyList<PermisoDirectoEntradaDto> Permisos);

public sealed record OpcionRol
{
    public long Id { get; init; }
    public string Nombre { get; init; } = "";
}

public sealed record OpcionesUsuarioDto(IReadOnlyList<OpcionRol> Roles, IReadOnlyList<EmpresaResumen> Empresas, IReadOnlyList<SucursalResumen> Sucursales);

public sealed record ConsultaUsuarios : ConsultaPaginada
{
    public bool? Activo { get; init; }
}

// ----- Validadores -----

public sealed class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.NombreUsuario).NotEmpty().Length(3, 50)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("El usuario solo admite letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.Correo).NotEmpty().MaximumLength(150).EmailAddress();
        RuleFor(x => x.NombreCompleto).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PasswordTemporal).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
        RuleFor(x => x.RolIds).NotNull();
        RuleFor(x => x.EmpresaIds).NotEmpty().WithMessage("Seleccione al menos una empresa.");
        RuleFor(x => x.SucursalIds).NotEmpty().WithMessage("Seleccione al menos una sucursal.");
    }
}

public sealed class EditarUsuarioValidator : AbstractValidator<EditarUsuarioDto>
{
    public EditarUsuarioValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().MaximumLength(150).EmailAddress();
        RuleFor(x => x.NombreCompleto).NotEmpty().MaximumLength(150);
        RuleFor(x => x.RolIds).NotNull();
        RuleFor(x => x.EmpresaIds).NotEmpty().WithMessage("Seleccione al menos una empresa.");
        RuleFor(x => x.SucursalIds).NotEmpty().WithMessage("Seleccione al menos una sucursal.");
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

public sealed class RestablecerPasswordAdminValidator : AbstractValidator<RestablecerPasswordAdminDto>
{
    public RestablecerPasswordAdminValidator() =>
        RuleFor(x => x.PasswordTemporal).NotEmpty().MaximumLength(PoliticaPassword.LongitudMaxima);
}

public sealed class GuardarPermisosDirectosValidator : AbstractValidator<GuardarPermisosDirectosDto>
{
    public GuardarPermisosDirectosValidator()
    {
        RuleFor(x => x.Permisos).NotNull();
        RuleForEach(x => x.Permisos).ChildRules(p =>
        {
            p.RuleFor(x => x.PermisoId).GreaterThan(0);
            p.RuleFor(x => x.Efecto).IsEnumName(typeof(EfectoPermiso)).WithMessage("El efecto debe ser CONCEDER o DENEGAR.");
            p.RuleFor(x => x.Motivo).MaximumLength(300);
        });
        RuleFor(x => x.Permisos)
            .Must(p => p is null || p.Select(i => i.PermisoId).Distinct().Count() == p.Count)
            .WithMessage("Un permiso no puede aparecer más de una vez.");
    }
}

// ----- Puerto -----

public interface IUsuarioRepository
{
    Task<ResultadoPaginado<UsuarioListaDto>> ListarAsync(ConsultaUsuarios consulta, CancellationToken ct);

    Task<UsuarioDetalleDto?> ObtenerAsync(long id, CancellationToken ct);

    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, CancellationToken ct);

    Task<bool> ExisteCorreoAsync(string correo, long? exceptoId, CancellationToken ct);

    /// <summary>Inserta sin contraseña utilizable; el hash se establece después en la misma transacción.</summary>
    Task<long> InsertarAsync(string nombreUsuario, string correo, string nombreCompleto, long creadoPor, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long id, string correo, string nombreCompleto, bool activo, uint versionFila, long modificadoPor, DateTime ahoraUtc, CancellationToken ct);

    Task ReemplazarRolesAsync(long id, IReadOnlyCollection<long> rolIds, long por, CancellationToken ct);

    /// <summary>La primera empresa y la primera sucursal de cada lista quedan como predeterminadas.</summary>
    Task ReemplazarAccesosAsync(long id, IReadOnlyList<long> empresaIds, IReadOnlyList<long> sucursalIds, long por, CancellationToken ct);

    Task<IReadOnlyList<PermisoDirectoDto>> ObtenerPermisosDirectosAsync(long id, CancellationToken ct);

    Task ReemplazarPermisosDirectosAsync(long id, IReadOnlyList<PermisoDirectoEntradaDto> permisos, long por, DateTime ahoraUtc, CancellationToken ct);

    /// <summary>Usuarios activos con un rol de sistema activo; evita dejar el sistema sin administrador.</summary>
    Task<int> ContarAdministradoresActivosAsync(CancellationToken ct);

    Task<IReadOnlyList<OpcionRol>> ListarRolesActivosAsync(CancellationToken ct);

    Task<int> ContarPermisosExistentesAsync(IReadOnlyCollection<long> permisoIds, CancellationToken ct);
}

// ----- Caso de uso -----

public sealed class UsuariosService(
    IUsuarioRepository repositorio,
    AutenticacionService autenticacion,
    ISesionRepository sesiones,
    IAutorizacionRepository autorizacion,
    IOrganizacionConsulta organizacion,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    private const string Modulo = "SEGURIDAD";
    private const string Entidad = "usuario";

    public Task<ResultadoPaginado<UsuarioListaDto>> ListarAsync(ConsultaUsuarios consulta, CancellationToken ct) =>
        repositorio.ListarAsync(consulta, ct);

    public async Task<UsuarioDetalleDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El usuario no existe.");

    public async Task<OpcionesUsuarioDto> ObtenerOpcionesAsync(CancellationToken ct) =>
        new(await repositorio.ListarRolesActivosAsync(ct),
            await organizacion.ListarEmpresasAsync(ct),
            await organizacion.ListarSucursalesAsync(ct));

    public Task<UsuarioDetalleDto> CrearAsync(CrearUsuarioDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();
        var nombreUsuario = dto.NombreUsuario.Trim().ToLowerInvariant();
        var correo = dto.Correo.Trim().ToLowerInvariant();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (await repositorio.ExisteNombreUsuarioAsync(nombreUsuario, ct))
                throw new ConflictoException("DUPLICADO", $"Ya existe el usuario {nombreUsuario}.");
            if (await repositorio.ExisteCorreoAsync(correo, null, ct))
                throw new ConflictoException("DUPLICADO", "Ya existe un usuario con ese correo.");

            var (empresas, sucursales) = await ValidarAsignacionesAsync(dto.RolIds, dto.EmpresaIds, dto.SucursalIds, ct);

            var id = await repositorio.InsertarAsync(nombreUsuario, correo, dto.NombreCompleto.Trim(), por, reloj.UtcNow, ct);
            await autenticacion.EstablecerPasswordAsync(id, dto.PasswordTemporal, "passwordTemporal", por, ct, debeCambiar: true);
            await repositorio.ReemplazarRolesAsync(id, dto.RolIds.Distinct().ToList(), por, ct);
            await repositorio.ReemplazarAccesosAsync(id, empresas, sucursales, por, ct);
            await autorizacion.IncrementarVersionAsync(ct);

            var creado = (await repositorio.ObtenerAsync(id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creado), ct);
            return creado;
        }, ct);
    }

    public Task<UsuarioDetalleDto> EditarAsync(long id, EditarUsuarioDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();
        var correo = dto.Correo.Trim().ToLowerInvariant();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El usuario no existe.");

            if (id == por && !dto.Activo)
                throw new ReglaNegocioException("USUARIO_PROPIO", "No puede desactivar su propio usuario.");
            if (await repositorio.ExisteCorreoAsync(correo, id, ct))
                throw new ConflictoException("DUPLICADO", "Ya existe un usuario con ese correo.");

            var (empresas, sucursales) = await ValidarAsignacionesAsync(dto.RolIds, dto.EmpresaIds, dto.SucursalIds, ct);

            if (!await repositorio.ActualizarAsync(id, correo, dto.NombreCompleto.Trim(), dto.Activo, dto.VersionFila, por, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            await repositorio.ReemplazarRolesAsync(id, dto.RolIds.Distinct().ToList(), por, ct);
            await repositorio.ReemplazarAccesosAsync(id, empresas, sucursales, por, ct);

            if (await repositorio.ContarAdministradoresActivosAsync(ct) == 0)
                throw new ReglaNegocioException("SIN_ADMINISTRADOR", "Debe quedar al menos un usuario activo con rol de administrador.");

            if (!dto.Activo)
                await sesiones.RevocarDeUsuarioAsync(id, MotivosRevocacion.UsuarioInactivo, reloj.UtcNow, ct);
            await autorizacion.IncrementarVersionAsync(ct);

            var actualizado = (await repositorio.ObtenerAsync(id, ct))!;
            var accion = anterior.Activo && !actualizado.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizado), ct);
            return actualizado;
        }, ct);
    }

    /// <summary>Baja lógica: el usuario queda inactivo y se cierran sus sesiones.</summary>
    public async Task DesactivarAsync(long id, uint versionFila, CancellationToken ct)
    {
        var existente = await ObtenerAsync(id, ct);
        await EditarAsync(id, new EditarUsuarioDto(existente.Correo, existente.NombreCompleto, false,
            existente.RolIds, existente.EmpresaIds, existente.SucursalIds, versionFila), ct);
    }

    /// <summary>El administrador asigna una contraseña temporal; el usuario deberá cambiarla al ingresar.</summary>
    public Task RestablecerPasswordAsync(long id, RestablecerPasswordAdminDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            _ = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El usuario no existe.");
            await autenticacion.EstablecerPasswordAsync(id, dto.PasswordTemporal, "passwordTemporal", por, ct, debeCambiar: true);
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "RESTABLECER_PASSWORD",
                Mensaje: "Contraseña temporal asignada por un administrador"), ct);
        }, ct);
    }

    public async Task<IReadOnlyList<PermisoDirectoDto>> ObtenerPermisosDirectosAsync(long id, CancellationToken ct)
    {
        _ = await ObtenerAsync(id, ct);
        return await repositorio.ObtenerPermisosDirectosAsync(id, ct);
    }

    public Task<IReadOnlyList<PermisoDirectoDto>> GuardarPermisosDirectosAsync(long id, GuardarPermisosDirectosDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            _ = await repositorio.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("El usuario no existe.");

            var ahora = reloj.UtcNow;
            if (dto.Permisos.Any(p => p.VigenteHasta is { } hasta && hasta <= ahora))
                throw new ValidacionException("permisos", "La vigencia de un permiso debe terminar en el futuro.");

            var ids = dto.Permisos.Select(p => p.PermisoId).ToList();
            if (await repositorio.ContarPermisosExistentesAsync(ids, ct) != ids.Count)
                throw new ValidacionException("permisos", "Uno o más permisos no existen.");

            var anteriores = await repositorio.ObtenerPermisosDirectosAsync(id, ct);
            await repositorio.ReemplazarPermisosDirectosAsync(id, dto.Permisos, por, ahora, ct);

            if (await repositorio.ContarAdministradoresActivosAsync(ct) == 0)
                throw new ReglaNegocioException("SIN_ADMINISTRADOR", "Debe quedar al menos un usuario activo con rol de administrador.");

            await autorizacion.IncrementarVersionAsync(ct);
            var nuevos = await repositorio.ObtenerPermisosDirectosAsync(id, ct);
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, "usuario_permiso", id.ToString(), "EDITAR", anteriores, nuevos), ct);
            return nuevos;
        }, ct);
    }

    /// <summary>
    /// Comprueba que roles, empresas y sucursales existan y estén activos, y que cada sucursal
    /// pertenezca a una de las empresas elegidas. Devuelve las listas sin repetidos, en el orden recibido.
    /// </summary>
    private async Task<(IReadOnlyList<long> Empresas, IReadOnlyList<long> Sucursales)> ValidarAsignacionesAsync(
        IReadOnlyList<long> rolIds, IReadOnlyList<long> empresaIds, IReadOnlyList<long> sucursalIds, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();

        var rolesActivos = (await repositorio.ListarRolesActivosAsync(ct)).Select(r => r.Id).ToHashSet();
        if (rolIds.Any(r => !rolesActivos.Contains(r)))
            errores["rolIds"] = ["Uno o más roles no existen o están inactivos."];

        var empresas = empresaIds.Distinct().ToList();
        var empresasActivas = (await organizacion.ListarEmpresasAsync(ct)).Select(e => e.Id).ToHashSet();
        if (empresas.Any(e => !empresasActivas.Contains(e)))
            errores["empresaIds"] = ["Una o más empresas no existen o están inactivas."];

        var sucursales = sucursalIds.Distinct().ToList();
        var empresaDeSucursal = (await organizacion.ListarSucursalesAsync(ct)).ToDictionary(s => s.Id, s => s.EmpresaId);
        if (sucursales.Any(s => !empresaDeSucursal.TryGetValue(s, out var empresa) || !empresas.Contains(empresa)))
            errores["sucursalIds"] = ["Cada sucursal debe existir, estar activa y pertenecer a una de las empresas seleccionadas."];
        else if (empresas.Any(e => !sucursales.Any(s => empresaDeSucursal[s] == e)))
            errores["sucursalIds"] = ["Seleccione al menos una sucursal de cada empresa."];

        if (errores.Count > 0)
            throw new ValidacionException(errores);

        return (empresas, sucursales);
    }
}
