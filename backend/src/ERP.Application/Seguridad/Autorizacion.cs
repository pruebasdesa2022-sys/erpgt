using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Domain.Comun;
using ERP.Domain.Seguridad;

namespace ERP.Application.Seguridad;

/// <summary>
/// Todo lo necesario para autorizar a un usuario en una empresa. Se guarda en caché por
/// (usuario, empresa, versión de seguridad); la vigencia de los permisos directos se evalúa en cada uso.
/// </summary>
public sealed record DatosAutorizacion(
    bool Activo,
    bool DebeCambiarPassword,
    IReadOnlySet<long> Empresas,
    IReadOnlyDictionary<long, long> EmpresaDeSucursal,
    IReadOnlySet<string> PermisosDeRoles,
    IReadOnlyList<PermisoDirecto> PermisosDirectos,
    IReadOnlyDictionary<string, string> ModuloDePermiso,
    IReadOnlyDictionary<string, EstadoModulo> EstadoEfectivoDeModulo);

public interface IAutorizacionRepository
{
    Task<long> ObtenerVersionAsync(CancellationToken ct);

    /// <summary>Invalida la caché de autorización. Se llama dentro de la transacción que cambia roles, permisos, usuarios o módulos.</summary>
    Task IncrementarVersionAsync(CancellationToken ct);

    /// <summary>Devuelve null si el usuario no existe. Puede servir desde caché para la misma versión.</summary>
    Task<DatosAutorizacion?> CargarAsync(long usuarioId, long empresaId, long version, CancellationToken ct);
}

public sealed class AutorizacionService(IAutorizacionRepository repositorio, IUsuarioActual actual, IClock reloj)
    : IAccesosUsuarioActual
{
    private DatosAutorizacion? _datos;

    /// <summary>
    /// Pasos 2 a 4 de la cadena de seguridad: contexto empresa/sucursal → módulo habilitado → permiso.
    /// Con <paramref name="permiso"/> nulo solo valida usuario activo y contexto.
    /// </summary>
    public async Task VerificarAsync(string? permiso, CancellationToken ct)
    {
        var datos = await ObtenerAsync(ct);

        if (permiso is null)
            return;

        if (datos.DebeCambiarPassword)
            throw new AccesoDenegadoException("CAMBIO_PASSWORD_REQUERIDO", "Debe cambiar su contraseña antes de continuar.");

        if (!datos.ModuloDePermiso.TryGetValue(permiso, out var modulo))
            throw new AccesoDenegadoException("PERMISO_DENEGADO", "No tiene permiso para realizar esta acción.");

        var efectivos = ResolucionPermisos.Resolver(datos.PermisosDeRoles, datos.PermisosDirectos, reloj.UtcNow);

        if (!ModuloAccesible(datos, modulo, efectivos))
            throw new AccesoDenegadoException("MODULO_NO_DISPONIBLE", "El módulo no está disponible.");

        if (!efectivos.Contains(permiso))
            throw new AccesoDenegadoException("PERMISO_DENEGADO", "No tiene permiso para realizar esta acción.");
    }

    /// <summary>Permisos que el usuario puede ejercer ahora: los efectivos cuyo módulo está accesible.</summary>
    public async Task<IReadOnlyList<string>> PermisosEjerciblesAsync(CancellationToken ct)
    {
        var datos = await ObtenerAsync(ct);
        if (datos.DebeCambiarPassword)
            return [];

        var efectivos = ResolucionPermisos.Resolver(datos.PermisosDeRoles, datos.PermisosDirectos, reloj.UtcNow);
        return efectivos
            .Where(p => datos.ModuloDePermiso.TryGetValue(p, out var modulo) && ModuloAccesible(datos, modulo, efectivos))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<DatosAutorizacion> ObtenerAsync(CancellationToken ct)
    {
        if (_datos is not null)
            return _datos;

        var usuarioId = actual.UsuarioIdRequerido();
        var empresaId = actual.EmpresaIdRequerida();
        var version = await repositorio.ObtenerVersionAsync(ct);
        var datos = await repositorio.CargarAsync(usuarioId, empresaId, version, ct);

        if (datos is null || !datos.Activo)
            throw new NoAutenticadoException("SESION_INVALIDA", "La sesión ya no es válida.");

        // El token trae la empresa y la sucursal, pero se revalidan aquí: quitarle un acceso
        // a un usuario surte efecto aunque su token siga vigente.
        var sucursalValida = actual.SucursalId is { } sucursalId
            && datos.EmpresaDeSucursal.TryGetValue(sucursalId, out var empresaDeSucursal)
            && empresaDeSucursal == empresaId;

        if (!datos.Empresas.Contains(empresaId) || !sucursalValida)
            throw new AccesoDenegadoException("CONTEXTO_INVALIDO", "No tiene acceso a la empresa o sucursal seleccionada.");

        return _datos = datos;
    }

    public async Task<IReadOnlyCollection<long>> EmpresasPermitidasAsync(CancellationToken ct) =>
        (await ObtenerAsync(ct)).Empresas.ToList();

    private static bool ModuloAccesible(DatosAutorizacion datos, string modulo, IReadOnlySet<string> efectivos)
    {
        if (!datos.EstadoEfectivoDeModulo.TryGetValue(modulo, out var estado))
            return false;

        return ReglasModulo.Acceso(estado) switch
        {
            AccesoModulo.Disponible => true,
            AccesoModulo.SoloPruebas => efectivos.Contains(ReglasModulo.PermisoPruebas(modulo)),
            _ => false,
        };
    }
}
