using ERP.Application.Comun;
using ERP.Application.Seguridad;
using ERP.Application.Seguridad.Contratos;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Seguridad;

public sealed class UsuarioRepository(UnitOfWork unidadDeTrabajo)
    : RepositorioBase(unidadDeTrabajo), IUsuarioRepository, IUsuariosConsulta
{
    private const string Filtro =
        """
        WHERE (@activo IS NULL OR u.activo = @activo)
          AND (@patron IS NULL OR u.nombre_usuario LIKE @patron OR u.nombre_completo LIKE @patron OR u.correo LIKE @patron)
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nombreUsuario"] = "u.nombre_usuario",
        ["nombreCompleto"] = "u.nombre_completo",
        ["correo"] = "u.correo",
        ["activo"] = "u.activo",
        ["ultimoAccesoEn"] = "u.ultimo_acceso_en",
    };

    public async Task<ResultadoPaginado<UsuarioListaDto>> ListarAsync(ConsultaUsuarios consulta, CancellationToken ct)
    {
        var parametros = new
        {
            activo = consulta.Activo,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado,
            desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "u.nombre_usuario ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM usuario u {Filtro}", parametros, ct);
        var items = await ConsultarAsync<UsuarioListaDto>(
            $"""
            SELECT u.id, u.nombre_usuario, u.correo, u.nombre_completo, u.activo, u.debe_cambiar_password,
                   u.bloqueado_hasta, u.ultimo_acceso_en, u.version_fila,
                   (SELECT GROUP_CONCAT(r.nombre ORDER BY r.nombre SEPARATOR ', ')
                    FROM usuario_rol ur JOIN rol r ON r.id = ur.rol_id
                    WHERE ur.usuario_id = u.id) AS roles
            FROM usuario u {Filtro}
            ORDER BY {orden}, u.id
            LIMIT @limite OFFSET @desplazamiento
            """, parametros, ct);

        return new ResultadoPaginado<UsuarioListaDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public async Task<UsuarioDetalleDto?> ObtenerAsync(long id, CancellationToken ct)
    {
        var usuario = await ConsultarUnoAsync<UsuarioDetalleDto>(
            $"""
            SELECT id, nombre_usuario, correo, nombre_completo, activo, debe_cambiar_password, bloqueado_hasta, ultimo_acceso_en, version_fila
            FROM usuario WHERE id = @id{ParaActualizar}
            """, new { id }, ct);
        if (usuario is null)
            return null;

        return usuario with
        {
            RolIds = await ConsultarAsync<long>(
                "SELECT CAST(rol_id AS SIGNED) FROM usuario_rol WHERE usuario_id = @id ORDER BY rol_id", new { id }, ct),
            EmpresaIds = await ConsultarAsync<long>(
                "SELECT CAST(empresa_id AS SIGNED) FROM usuario_empresa WHERE usuario_id = @id ORDER BY es_predeterminada DESC, id", new { id }, ct),
            SucursalIds = await ConsultarAsync<long>(
                "SELECT CAST(sucursal_id AS SIGNED) FROM usuario_sucursal WHERE usuario_id = @id ORDER BY es_predeterminada DESC, id", new { id }, ct),
        };
    }

    public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, CancellationToken ct) =>
        await EscalarAsync<long>("SELECT COUNT(*) FROM usuario WHERE nombre_usuario = @nombreUsuario", new { nombreUsuario }, ct) > 0;

    public async Task<bool> ExisteCorreoAsync(string correo, long? exceptoId, CancellationToken ct) =>
        await EscalarAsync<long>(
            "SELECT COUNT(*) FROM usuario WHERE correo = @correo AND (@exceptoId IS NULL OR id <> @exceptoId)", new { correo, exceptoId }, ct) > 0;

    public Task<long> InsertarAsync(string nombreUsuario, string correo, string nombreCompleto, long creadoPor, DateTime ahoraUtc, CancellationToken ct) =>
        InsertarAsync(
            """
            INSERT INTO usuario (nombre_usuario, correo, nombre_completo, password_hash, debe_cambiar_password, creado_en, creado_por)
            VALUES (@nombreUsuario, @correo, @nombreCompleto, '!', 1, @ahoraUtc, @creadoPor)
            """,
            new { nombreUsuario, correo, nombreCompleto, ahoraUtc, creadoPor }, ct);

    public async Task<bool> ActualizarAsync(long id, string correo, string nombreCompleto, bool activo, uint versionFila, long modificadoPor, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE usuario
            SET correo = @correo, nombre_completo = @nombreCompleto, activo = @activo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @modificadoPor
            WHERE id = @id AND version_fila = @versionFila
            """,
            new { id, correo, nombreCompleto, activo, versionFila, modificadoPor, ahoraUtc }, ct) == 1;

    public async Task ReemplazarRolesAsync(long id, IReadOnlyCollection<long> rolIds, long por, CancellationToken ct)
    {
        await EjecutarAsync("DELETE FROM usuario_rol WHERE usuario_id = @id", new { id }, ct);
        if (rolIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO usuario_rol (usuario_id, rol_id, empresa_id, creado_por) VALUES (@id, @rolId, NULL, @por)",
                rolIds.Select(rolId => new { id, rolId, por }), ct);
    }

    public async Task ReemplazarAccesosAsync(long id, IReadOnlyList<long> empresaIds, IReadOnlyList<long> sucursalIds, long por, CancellationToken ct)
    {
        await EjecutarAsync("DELETE FROM usuario_sucursal WHERE usuario_id = @id", new { id }, ct);
        await EjecutarAsync("DELETE FROM usuario_empresa WHERE usuario_id = @id", new { id }, ct);

        if (empresaIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO usuario_empresa (usuario_id, empresa_id, es_predeterminada, creado_por) VALUES (@id, @empresaId, @predeterminada, @por)",
                empresaIds.Select((empresaId, i) => new { id, empresaId, predeterminada = i == 0, por }), ct);
        if (sucursalIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO usuario_sucursal (usuario_id, sucursal_id, es_predeterminada, creado_por) VALUES (@id, @sucursalId, @predeterminada, @por)",
                sucursalIds.Select((sucursalId, i) => new { id, sucursalId, predeterminada = i == 0, por }), ct);
    }

    public Task<IReadOnlyList<PermisoDirectoDto>> ObtenerPermisosDirectosAsync(long id, CancellationToken ct) =>
        ConsultarAsync<PermisoDirectoDto>(
            """
            SELECT up.permiso_id, p.codigo, up.efecto, up.vigente_hasta, up.motivo
            FROM usuario_permiso up JOIN permiso p ON p.id = up.permiso_id
            WHERE up.usuario_id = @id AND up.activo = 1
            ORDER BY p.codigo
            """, new { id }, ct);

    public async Task ReemplazarPermisosDirectosAsync(long id, IReadOnlyList<PermisoDirectoEntradaDto> permisos, long por, DateTime ahoraUtc, CancellationToken ct)
    {
        await EjecutarAsync("DELETE FROM usuario_permiso WHERE usuario_id = @id", new { id }, ct);
        if (permisos.Count > 0)
            await EjecutarAsync(
                """
                INSERT INTO usuario_permiso (usuario_id, permiso_id, efecto, vigente_desde, vigente_hasta, motivo, creado_en, creado_por)
                VALUES (@id, @PermisoId, @Efecto, @ahoraUtc, @VigenteHasta, @Motivo, @ahoraUtc, @por)
                """,
                permisos.Select(p => new { id, p.PermisoId, p.Efecto, p.VigenteHasta, Motivo = string.IsNullOrWhiteSpace(p.Motivo) ? null : p.Motivo.Trim(), ahoraUtc, por }), ct);
    }

    public async Task<int> ContarAdministradoresActivosAsync(CancellationToken ct) =>
        (int)await EscalarAsync<long>(
            """
            SELECT COUNT(DISTINCT u.id)
            FROM usuario u
            JOIN usuario_rol ur ON ur.usuario_id = u.id
            JOIN rol r ON r.id = ur.rol_id
            WHERE u.activo = 1 AND r.activo = 1 AND r.es_sistema = 1
            """, null, ct);

    public Task<IReadOnlyList<OpcionRol>> ListarRolesActivosAsync(CancellationToken ct) =>
        ConsultarAsync<OpcionRol>("SELECT id, nombre FROM rol WHERE activo = 1 ORDER BY nombre", null, ct);

    public async Task<int> ContarPermisosExistentesAsync(IReadOnlyCollection<long> permisoIds, CancellationToken ct) =>
        permisoIds.Count == 0
            ? 0
            : (int)await EscalarAsync<long>("SELECT COUNT(*) FROM permiso WHERE activo = 1 AND id IN @permisoIds", new { permisoIds }, ct);

    public async Task<IReadOnlyDictionary<long, string>> ObtenerNombresAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<long, string>();

        var filas = await ConsultarAsync<OpcionRol>("SELECT id, nombre_usuario AS nombre FROM usuario WHERE id IN @ids", new { ids }, ct);
        return filas.ToDictionary(f => f.Id, f => f.Nombre);
    }
}

public sealed class RolRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IRolRepository
{
    private const string Filtro =
        """
        WHERE (@activo IS NULL OR r.activo = @activo)
          AND (@patron IS NULL OR r.codigo LIKE @patron OR r.nombre LIKE @patron)
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codigo"] = "r.codigo",
        ["nombre"] = "r.nombre",
        ["activo"] = "r.activo",
    };

    public async Task<ResultadoPaginado<RolListaDto>> ListarAsync(ConsultaRoles consulta, CancellationToken ct)
    {
        var parametros = new
        {
            activo = consulta.Activo,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado,
            desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "r.nombre ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM rol r {Filtro}", parametros, ct);
        var items = await ConsultarAsync<RolListaDto>(
            $"""
            SELECT r.id, r.codigo, r.nombre, r.descripcion, r.es_sistema, r.activo, r.version_fila,
                   (SELECT COUNT(*) FROM rol_permiso rp WHERE rp.rol_id = r.id) AS cantidad_permisos,
                   (SELECT COUNT(*) FROM usuario_rol ur WHERE ur.rol_id = r.id) AS cantidad_usuarios
            FROM rol r {Filtro}
            ORDER BY {orden}, r.id
            LIMIT @limite OFFSET @desplazamiento
            """, parametros, ct);

        return new ResultadoPaginado<RolListaDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public async Task<RolDetalleDto?> ObtenerAsync(long id, CancellationToken ct)
    {
        var rol = await ConsultarUnoAsync<RolDetalleDto>(
            $"SELECT id, codigo, nombre, descripcion, es_sistema, activo, version_fila FROM rol WHERE id = @id{ParaActualizar}", new { id }, ct);
        if (rol is null)
            return null;

        return rol with
        {
            PermisoIds = await ConsultarAsync<long>(
                "SELECT CAST(permiso_id AS SIGNED) FROM rol_permiso WHERE rol_id = @id ORDER BY permiso_id", new { id }, ct),
        };
    }

    public async Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct) =>
        await EscalarAsync<long>("SELECT COUNT(*) FROM rol WHERE codigo = @codigo", new { codigo }, ct) > 0;

    public Task<long> InsertarAsync(string codigo, string nombre, string? descripcion, long creadoPor, DateTime ahoraUtc, CancellationToken ct) =>
        InsertarAsync(
            "INSERT INTO rol (codigo, nombre, descripcion, creado_en, creado_por) VALUES (@codigo, @nombre, @descripcion, @ahoraUtc, @creadoPor)",
            new { codigo, nombre, descripcion, ahoraUtc, creadoPor }, ct);

    public async Task<bool> ActualizarAsync(long id, string nombre, string? descripcion, bool activo, uint versionFila, long modificadoPor, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE rol
            SET nombre = @nombre, descripcion = @descripcion, activo = @activo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @modificadoPor
            WHERE id = @id AND version_fila = @versionFila
            """,
            new { id, nombre, descripcion, activo, versionFila, modificadoPor, ahoraUtc }, ct) == 1;

    public async Task ReemplazarPermisosAsync(long id, IReadOnlyCollection<long> permisoIds, long por, CancellationToken ct)
    {
        await EjecutarAsync("DELETE FROM rol_permiso WHERE rol_id = @id", new { id }, ct);
        if (permisoIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO rol_permiso (rol_id, permiso_id, creado_por) VALUES (@id, @permisoId, @por)",
                permisoIds.Select(permisoId => new { id, permisoId, por }), ct);
    }

    public async Task<int> ContarUsuariosActivosAsync(long id, CancellationToken ct) =>
        (int)await EscalarAsync<long>(
            "SELECT COUNT(*) FROM usuario_rol ur JOIN usuario u ON u.id = ur.usuario_id WHERE ur.rol_id = @id AND u.activo = 1", new { id }, ct);

    public Task<IReadOnlyList<PermisoDto>> ListarPermisosAsync(CancellationToken ct) =>
        ConsultarAsync<PermisoDto>(
            """
            SELECT p.id, p.codigo, m.codigo AS modulo, m.nombre AS modulo_nombre, p.recurso, p.accion, p.nombre
            FROM permiso p JOIN modulo m ON m.id = p.modulo_id
            WHERE p.activo = 1
            ORDER BY m.orden, p.recurso, p.id
            """, null, ct);
}

public sealed class ModuloRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IModuloRepository, IMenuRepository
{
    private const string Seleccion =
        """
        SELECT m.id, m.codigo, m.nombre, m.fase, m.orden, m.estado_global, me.estado AS estado_empresa, m.es_base,
               m.version_fila, me.version_fila AS version_fila_empresa
        FROM modulo m
        LEFT JOIN modulo_empresa me ON me.modulo_id = m.id AND me.empresa_id = @empresaId
        WHERE m.activo = 1
        """;

    public Task<IReadOnlyList<ModuloFila>> ListarAsync(long empresaId, CancellationToken ct) =>
        ConsultarAsync<ModuloFila>($"{Seleccion} ORDER BY m.fase, m.orden", new { empresaId }, ct);

    public Task<ModuloFila?> ObtenerAsync(long id, long empresaId, CancellationToken ct) =>
        ConsultarUnoAsync<ModuloFila>($"{Seleccion} AND m.id = @id", new { id, empresaId }, ct);

    public async Task<bool> ActualizarGlobalAsync(long id, string estado, uint versionFila, long por, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE modulo
            SET estado_global = @estado, version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @por
            WHERE id = @id AND version_fila = @versionFila
            """,
            new { id, estado, versionFila, por, ahoraUtc }, ct) == 1;

    public async Task<bool> GuardarEstadoEmpresaAsync(long id, long empresaId, string estado, string motivo, uint versionFila, long por, DateTime ahoraUtc, CancellationToken ct)
    {
        var parametros = new { id, empresaId, estado, motivo, versionFila, por, ahoraUtc };

        if (versionFila == 0)
            return await EjecutarAsync(
                """
                INSERT INTO modulo_empresa (modulo_id, empresa_id, estado, vigente_desde, cambiado_por, motivo, creado_en, creado_por)
                SELECT @id, @empresaId, @estado, @ahoraUtc, @por, @motivo, @ahoraUtc, @por
                FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM modulo_empresa WHERE modulo_id = @id AND empresa_id = @empresaId)
                """, parametros, ct) == 1;

        return await EjecutarAsync(
            """
            UPDATE modulo_empresa
            SET estado = @estado, vigente_desde = @ahoraUtc, cambiado_por = @por, motivo = @motivo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @por
            WHERE modulo_id = @id AND empresa_id = @empresaId AND version_fila = @versionFila
            """, parametros, ct) == 1;
    }

    public Task<IReadOnlyList<MenuFila>> ListarActivosAsync(CancellationToken ct) =>
        ConsultarAsync<MenuFila>(
            """
            SELECT mn.id, mn.padre_id, mn.tipo, mn.codigo, mn.nombre, mn.icono, mn.ruta, mn.orden, p.codigo AS permiso_codigo
            FROM menu mn
            LEFT JOIN permiso p ON p.id = mn.permiso_requerido_id
            WHERE mn.activo = 1
            """, null, ct);
}
