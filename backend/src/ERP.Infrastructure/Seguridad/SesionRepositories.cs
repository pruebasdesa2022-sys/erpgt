using ERP.Application.Organizacion;
using ERP.Application.Seguridad;
using ERP.Domain.Seguridad;
using ERP.Infrastructure.Persistencia;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Infrastructure.Seguridad;

public sealed class CredencialesRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), ICredencialesRepository
{
    private const string Columnas =
        """
        id, nombre_usuario, correo, nombre_completo, password_hash, debe_cambiar_password,
        password_cambiado_en, intentos_fallidos, bloqueado_hasta, activo
        """;

    public Task<UsuarioCredenciales?> BuscarAsync(string nombreUsuarioOCorreo, CancellationToken ct) =>
        ConsultarUnoAsync<UsuarioCredenciales>(
            $"SELECT {Columnas} FROM usuario WHERE nombre_usuario = @valor OR correo = @valor LIMIT 1{ParaActualizar}",
            new { valor = nombreUsuarioOCorreo }, ct);

    public Task<UsuarioCredenciales?> ObtenerAsync(long id, CancellationToken ct) =>
        ConsultarUnoAsync<UsuarioCredenciales>($"SELECT {Columnas} FROM usuario WHERE id = @id{ParaActualizar}", new { id }, ct);

    public Task RegistrarFalloAsync(long id, int intentosFallidos, DateTime? bloqueadoHasta, CancellationToken ct) =>
        EjecutarAsync(
            "UPDATE usuario SET intentos_fallidos = @intentosFallidos, bloqueado_hasta = COALESCE(@bloqueadoHasta, bloqueado_hasta) WHERE id = @id",
            new { id, intentosFallidos, bloqueadoHasta }, ct);

    public Task RegistrarExitoAsync(long id, DateTime ahoraUtc, CancellationToken ct) =>
        EjecutarAsync(
            "UPDATE usuario SET intentos_fallidos = 0, bloqueado_hasta = NULL, ultimo_acceso_en = @ahoraUtc WHERE id = @id",
            new { id, ahoraUtc }, ct);

    public Task MarcarDebeCambiarAsync(long id, CancellationToken ct) =>
        EjecutarAsync("UPDATE usuario SET debe_cambiar_password = 1 WHERE id = @id", new { id }, ct);

    public async Task CambiarPasswordAsync(long id, string hash, bool debeCambiar, DateTime ahoraUtc, long? modificadoPor, CancellationToken ct)
    {
        await EjecutarAsync(
            """
            UPDATE usuario
            SET password_hash = @hash, debe_cambiar_password = @debeCambiar, password_cambiado_en = @ahoraUtc,
                intentos_fallidos = 0, bloqueado_hasta = NULL,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @modificadoPor
            WHERE id = @id
            """,
            new { id, hash, debeCambiar, ahoraUtc, modificadoPor }, ct);

        await EjecutarAsync(
            "INSERT INTO password_historial (usuario_id, password_hash, creado_en) VALUES (@id, @hash, @ahoraUtc)",
            new { id, hash, ahoraUtc }, ct);
    }

    public Task<IReadOnlyList<string>> UltimosHashesAsync(long id, int cantidad, CancellationToken ct) =>
        ConsultarAsync<string>(
            "SELECT password_hash FROM password_historial WHERE usuario_id = @id ORDER BY creado_en DESC, id DESC LIMIT @cantidad",
            new { id, cantidad }, ct);

    public async Task<PoliticaPassword> ObtenerPoliticaAsync(CancellationToken ct)
    {
        var fila = await ConsultarUnoAsync<FilaPolitica>(
            """
            SELECT longitud_minima, requiere_mayuscula, requiere_minuscula, requiere_digito, requiere_simbolo,
                   historial_cantidad, expira_dias, max_intentos_fallidos, bloqueo_minutos, recuperacion_minutos
            FROM politica_password
            WHERE activo = 1
            ORDER BY id DESC
            LIMIT 1
            """, null, ct)
            ?? throw new InvalidOperationException("No hay una política de contraseñas activa en la tabla politica_password.");

        return new PoliticaPassword(fila.LongitudMinima, fila.RequiereMayuscula, fila.RequiereMinuscula, fila.RequiereDigito,
            fila.RequiereSimbolo, fila.HistorialCantidad, fila.ExpiraDias, fila.MaxIntentosFallidos, fila.BloqueoMinutos, fila.RecuperacionMinutos);
    }

    public async Task<AccesosUsuario> ObtenerAccesosAsync(long id, CancellationToken ct)
    {
        var empresas = await ConsultarAsync<AccesoEmpresa>(
            "SELECT empresa_id, es_predeterminada FROM usuario_empresa WHERE usuario_id = @id ORDER BY es_predeterminada DESC, id", new { id }, ct);
        var sucursales = await ConsultarAsync<AccesoSucursal>(
            "SELECT sucursal_id, es_predeterminada FROM usuario_sucursal WHERE usuario_id = @id ORDER BY es_predeterminada DESC, id", new { id }, ct);
        return new AccesosUsuario(empresas, sucursales);
    }

    private sealed class FilaPolitica
    {
        public int LongitudMinima { get; init; }
        public bool RequiereMayuscula { get; init; }
        public bool RequiereMinuscula { get; init; }
        public bool RequiereDigito { get; init; }
        public bool RequiereSimbolo { get; init; }
        public int HistorialCantidad { get; init; }
        public int ExpiraDias { get; init; }
        public int MaxIntentosFallidos { get; init; }
        public int BloqueoMinutos { get; init; }
        public int RecuperacionMinutos { get; init; }
    }
}

public sealed class SesionRepository(UnitOfWork unidadDeTrabajo, ERP.Application.Comun.IUsuarioActual actual, ERP.Application.Comun.IClock reloj)
    : RepositorioBase(unidadDeTrabajo), ISesionRepository
{
    public Task<long> InsertarRefreshAsync(NuevoRefreshToken token, CancellationToken ct) =>
        InsertarAsync(
            """
            INSERT INTO refresh_token (usuario_id, familia_id, token_hash, empresa_id, sucursal_id, expira_en, ip, user_agent, creado_en)
            VALUES (@UsuarioId, @FamiliaId, @TokenHash, @EmpresaId, @SucursalId, @ExpiraEnUtc, @ip, @userAgent, @ahora)
            """,
            new
            {
                token.UsuarioId,
                token.FamiliaId,
                token.TokenHash,
                token.EmpresaId,
                token.SucursalId,
                token.ExpiraEnUtc,
                ip = Recortar(actual.Ip, 45),
                userAgent = Recortar(actual.UserAgent, 300),
                ahora = reloj.UtcNow,
            }, ct);

    public Task<RefreshGuardado?> ObtenerRefreshAsync(string tokenHash, CancellationToken ct) =>
        ConsultarUnoAsync<RefreshGuardado>(
            $"SELECT id, usuario_id, familia_id, empresa_id, sucursal_id, expira_en, revocado_en FROM refresh_token WHERE token_hash = @tokenHash{ParaActualizar}",
            new { tokenHash }, ct);

    public Task RevocarAsync(long id, string motivo, long? reemplazadoPorId, DateTime ahoraUtc, CancellationToken ct) =>
        EjecutarAsync(
            "UPDATE refresh_token SET revocado_en = @ahoraUtc, motivo_revocacion = @motivo, reemplazado_por_id = @reemplazadoPorId WHERE id = @id AND revocado_en IS NULL",
            new { id, motivo, reemplazadoPorId, ahoraUtc }, ct);

    public Task RevocarFamiliaAsync(string familiaId, string motivo, DateTime ahoraUtc, CancellationToken ct) =>
        EjecutarAsync(
            "UPDATE refresh_token SET revocado_en = @ahoraUtc, motivo_revocacion = @motivo WHERE familia_id = @familiaId AND revocado_en IS NULL",
            new { familiaId, motivo, ahoraUtc }, ct);

    public Task RevocarDeUsuarioAsync(long usuarioId, string motivo, DateTime ahoraUtc, CancellationToken ct) =>
        EjecutarAsync(
            "UPDATE refresh_token SET revocado_en = @ahoraUtc, motivo_revocacion = @motivo WHERE usuario_id = @usuarioId AND revocado_en IS NULL",
            new { usuarioId, motivo, ahoraUtc }, ct);

    public Task RegistrarIntentoAsync(string nombreUsuario, long? usuarioId, bool exitoso, string? motivo, CancellationToken ct) =>
        EjecutarAsync(
            """
            INSERT INTO intento_login (nombre_usuario, usuario_id, exitoso, motivo, ip, user_agent, creado_en)
            VALUES (@nombreUsuario, @usuarioId, @exitoso, @motivo, @ip, @userAgent, @ahora)
            """,
            new
            {
                nombreUsuario = Recortar(nombreUsuario, 150),
                usuarioId,
                exitoso,
                motivo,
                ip = Recortar(actual.Ip, 45),
                userAgent = Recortar(actual.UserAgent, 300),
                ahora = reloj.UtcNow,
            }, ct);

    public Task InsertarTokenRecuperacionAsync(long usuarioId, string tokenHash, DateTime expiraEnUtc, CancellationToken ct) =>
        EjecutarAsync(
            "INSERT INTO token_recuperacion (usuario_id, token_hash, expira_en, ip, creado_en) VALUES (@usuarioId, @tokenHash, @expiraEnUtc, @ip, @ahora)",
            new { usuarioId, tokenHash, expiraEnUtc, ip = Recortar(actual.Ip, 45), ahora = reloj.UtcNow }, ct);

    public Task<TokenRecuperacionGuardado?> ObtenerTokenRecuperacionAsync(string tokenHash, CancellationToken ct) =>
        ConsultarUnoAsync<TokenRecuperacionGuardado>(
            $"SELECT id, usuario_id, expira_en, usado_en FROM token_recuperacion WHERE token_hash = @tokenHash{ParaActualizar}", new { tokenHash }, ct);

    public Task MarcarTokenRecuperacionUsadoAsync(long id, DateTime ahoraUtc, CancellationToken ct) =>
        EjecutarAsync("UPDATE token_recuperacion SET usado_en = @ahoraUtc WHERE id = @id", new { id, ahoraUtc }, ct);

    private static string? Recortar(string? valor, int maximo) =>
        valor is null || valor.Length <= maximo ? valor : valor[..maximo];
}

public sealed class AutorizacionRepository(UnitOfWork unidadDeTrabajo, IOrganizacionConsulta organizacion, IMemoryCache cache)
    : RepositorioBase(unidadDeTrabajo), IAutorizacionRepository
{
    public Task<long> ObtenerVersionAsync(CancellationToken ct) =>
        EscalarAsync<long>("SELECT CAST(version AS SIGNED) FROM seguridad_version WHERE id = 1", null, ct);

    public Task IncrementarVersionAsync(CancellationToken ct) =>
        EjecutarAsync("UPDATE seguridad_version SET version = version + 1, modificado_en = UTC_TIMESTAMP(6) WHERE id = 1", null, ct);

    public async Task<DatosAutorizacion?> CargarAsync(long usuarioId, long empresaId, long version, CancellationToken ct)
    {
        var clave = $"autorizacion:{usuarioId}:{empresaId}:{version}";
        if (cache.TryGetValue(clave, out DatosAutorizacion? enCache))
            return enCache;

        var usuario = await ConsultarUnoAsync<FilaUsuario>(
            "SELECT activo, debe_cambiar_password FROM usuario WHERE id = @usuarioId", new { usuarioId }, ct);
        if (usuario is null)
            return null;

        // Solo cuentan las empresas y sucursales activas; ese dato es de Organización.
        var empresasActivas = (await organizacion.ListarEmpresasAsync(ct)).Select(e => e.Id).ToHashSet();
        var sucursalesActivas = (await organizacion.ListarSucursalesAsync(ct)).ToDictionary(s => s.Id, s => s.EmpresaId);

        var empresas = (await ConsultarAsync<long>(
                "SELECT CAST(empresa_id AS SIGNED) FROM usuario_empresa WHERE usuario_id = @usuarioId", new { usuarioId }, ct))
            .Where(empresasActivas.Contains)
            .ToHashSet();

        var empresaDeSucursal = (await ConsultarAsync<long>(
                "SELECT CAST(sucursal_id AS SIGNED) FROM usuario_sucursal WHERE usuario_id = @usuarioId", new { usuarioId }, ct))
            .Where(s => sucursalesActivas.TryGetValue(s, out var empresa) && empresas.Contains(empresa))
            .ToDictionary(s => s, s => sucursalesActivas[s]);

        var deRoles = (await ConsultarAsync<string>(
            """
            SELECT DISTINCT p.codigo
            FROM usuario_rol ur
            JOIN rol r ON r.id = ur.rol_id AND r.activo = 1
            JOIN rol_permiso rp ON rp.rol_id = r.id
            JOIN permiso p ON p.id = rp.permiso_id AND p.activo = 1
            WHERE ur.usuario_id = @usuarioId AND (ur.empresa_id IS NULL OR ur.empresa_id = @empresaId)
            """, new { usuarioId, empresaId }, ct)).ToHashSet(StringComparer.Ordinal);

        var directos = (await ConsultarAsync<FilaDirecto>(
            """
            SELECT p.codigo, up.efecto, up.vigente_desde, up.vigente_hasta
            FROM usuario_permiso up
            JOIN permiso p ON p.id = up.permiso_id AND p.activo = 1
            WHERE up.usuario_id = @usuarioId AND up.activo = 1
            """, new { usuarioId }, ct))
            .Select(d => new PermisoDirecto(d.Codigo, Enum.Parse<EfectoPermiso>(d.Efecto), d.VigenteDesde, d.VigenteHasta))
            .ToList();

        var moduloDePermiso = (await ConsultarAsync<FilaPermisoModulo>(
            "SELECT p.codigo AS permiso, m.codigo AS modulo FROM permiso p JOIN modulo m ON m.id = p.modulo_id WHERE p.activo = 1", null, ct))
            .ToDictionary(f => f.Permiso, f => f.Modulo, StringComparer.Ordinal);

        var estados = (await ConsultarAsync<FilaEstado>(
            """
            SELECT m.codigo, m.estado_global, me.estado AS estado_empresa
            FROM modulo m
            LEFT JOIN modulo_empresa me ON me.modulo_id = m.id AND me.empresa_id = @empresaId
            WHERE m.activo = 1
            """, new { empresaId }, ct))
            .ToDictionary(
                f => f.Codigo,
                f => ReglasModulo.EstadoEfectivo(
                    Enum.Parse<EstadoModulo>(f.EstadoGlobal),
                    f.EstadoEmpresa is null ? null : Enum.Parse<EstadoModulo>(f.EstadoEmpresa)),
                StringComparer.Ordinal);

        var datos = new DatosAutorizacion(usuario.Activo, usuario.DebeCambiarPassword, empresas, empresaDeSucursal,
            deRoles, directos, moduloDePermiso, estados);

        // Dentro de una transacción los datos pueden no estar confirmados: no se guardan en caché.
        if (!EnTransaccion)
            cache.Set(clave, datos, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });

        return datos;
    }

    private sealed class FilaUsuario
    {
        public bool Activo { get; init; }
        public bool DebeCambiarPassword { get; init; }
    }

    private sealed class FilaDirecto
    {
        public string Codigo { get; init; } = "";
        public string Efecto { get; init; } = "";
        public DateTime VigenteDesde { get; init; }
        public DateTime? VigenteHasta { get; init; }
    }

    private sealed class FilaPermisoModulo
    {
        public string Permiso { get; init; } = "";
        public string Modulo { get; init; } = "";
    }

    private sealed class FilaEstado
    {
        public string Codigo { get; init; } = "";
        public string EstadoGlobal { get; init; } = "";
        public string? EstadoEmpresa { get; init; }
    }
}
