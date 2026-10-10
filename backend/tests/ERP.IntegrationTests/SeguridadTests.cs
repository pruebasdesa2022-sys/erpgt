using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ERP.Api.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace ERP.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public class InstalacionTests(ApiFixture api)
{
    [Fact]
    public async Task El_script_maestro_instala_el_esquema_vigente_y_registra_su_version()
    {
        var nombre = new MySqlConnector.MySqlConnectionStringBuilder(api.CadenaConexion).Database;
        // 23 tablas de Fase 0 + 22 de la etapa 1A.
        Assert.Equal(45, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @b", ("@b", nombre)));
        Assert.Equal("0.2.1", await api.EscalarAsync<string>("SELECT version FROM esquema_version WHERE script = 'database.sql'"));
        Assert.Equal("Organización", await api.EscalarAsync<string>("SELECT nombre FROM modulo WHERE codigo = 'ORGANIZACION'"));
    }

    [Fact]
    public async Task Toda_llave_foranea_tiene_indice()
    {
        var nombre = new MySqlConnector.MySqlConnectionStringBuilder(api.CadenaConexion).Database;
        var sinIndice = await api.EscalarAsync<int>(
            """
            SELECT COUNT(*) FROM information_schema.key_column_usage k
            WHERE k.table_schema = @b AND k.referenced_table_name IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM information_schema.statistics s
                              WHERE s.table_schema = k.table_schema AND s.table_name = k.table_name
                                AND s.column_name = k.column_name AND s.seq_in_index = 1)
            """, ("@b", nombre));
        Assert.Equal(0, sinIndice);
    }

    [Fact]
    public async Task Todos_los_permisos_usados_por_los_endpoints_existen_en_el_catalogo_sembrado()
    {
        var usados = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<RequierePermisoAttribute>()?.Codigo)
            .OfType<string>()
            .Distinct();

        foreach (var codigo in usados)
            Assert.True(await api.EscalarAsync<int>("SELECT COUNT(*) FROM permiso WHERE codigo = @c AND activo = 1", ("@c", codigo)) == 1,
                $"El permiso {codigo} no está sembrado en database.sql.");
    }

    [Fact]
    public async Task El_rol_administrador_tiene_todos_los_permisos()
    {
        var faltantes = await api.EscalarAsync<int>(
            """
            SELECT COUNT(*) FROM permiso p
            WHERE NOT EXISTS (SELECT 1 FROM rol_permiso rp JOIN rol r ON r.id = rp.rol_id
                              WHERE rp.permiso_id = p.id AND r.codigo = 'ADMINISTRADOR')
            """);
        Assert.Equal(0, faltantes);
    }
}

[Collection(ColeccionApi.Nombre)]
public class SeguridadTests(ApiFixture api)
{
    private const string Sucursales = "/api/v1/organizacion/sucursales";
    private const string Autenticacion = "/api/v1/seguridad/autenticacion";

    [Fact]
    public async Task La_salud_y_la_version_son_publicas()
    {
        var cliente = api.ClienteAnonimo();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health")).StatusCode);
        var version = await cliente.GetFromJsonAsync<JsonElement>("/health/version");
        Assert.Equal("0.1.0", version.GetProperty("version").GetString());
    }

    [Theory]
    [InlineData("/api/v1/organizacion/sucursales")]
    [InlineData("/api/v1/seguridad/usuarios")]
    [InlineData("/api/v1/seguridad/menu")]
    [InlineData("/api/v1/auditoria/bitacora")]
    public async Task Sin_token_responde_401_con_ProblemDetails(string ruta)
    {
        var respuesta = await api.ClienteAnonimo().GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("NO_AUTENTICADO", await respuesta.ErrorCodeAsync());
        Assert.True(respuesta.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Un_token_alterado_responde_401()
    {
        var cliente = api.ClienteAnonimo();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.firma-falsa");
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(Sucursales)).StatusCode);
    }

    [Fact]
    public async Task Las_credenciales_incorrectas_responden_lo_mismo_exista_o_no_el_usuario()
    {
        var cliente = api.ClienteAnonimo();
        var inexistente = await cliente.PostAsJsonAsync($"{Autenticacion}/login", new { nombreUsuario = "nadie", password = "Cualquiera.123!" });
        var incorrecta = await cliente.PostAsJsonAsync($"{Autenticacion}/login", new { nombreUsuario = "admin", password = "Cualquiera.123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        Assert.Equal("CREDENCIALES_INVALIDAS", await inexistente.ErrorCodeAsync());
        Assert.Equal("CREDENCIALES_INVALIDAS", await incorrecta.ErrorCodeAsync());
    }

    [Fact]
    public async Task Un_usuario_nuevo_debe_cambiar_su_contrasena_antes_de_usar_el_sistema()
    {
        var admin = await api.ClienteAdminAsync();
        var nombre = $"n{Guid.NewGuid():N}"[..10];
        var creado = await admin.PostAsJsonAsync("/api/v1/seguridad/usuarios", new
        {
            nombreUsuario = nombre, correo = $"{nombre}@pruebas.local", nombreCompleto = "Nuevo", passwordTemporal = "Temporal.2026!x",
            rolIds = Array.Empty<long>(), empresaIds = new[] { 1L }, sucursalIds = new[] { 1L },
        });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var tokens = await api.LoginAsync(nombre, "Temporal.2026!x");
        Assert.True(tokens.GetProperty("debeCambiarPassword").GetBoolean());

        var cliente = await api.IniciarSesionAsync(nombre, "Temporal.2026!x");
        var bloqueado = await cliente.GetAsync(Sucursales);
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Equal("CAMBIO_PASSWORD_REQUERIDO", await bloqueado.ErrorCodeAsync());
        Assert.Empty((await cliente.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/menu")).EnumerateArray());

        var debil = await cliente.PostAsJsonAsync($"{Autenticacion}/cambiar-password", new { passwordActual = "Temporal.2026!x", passwordNueva = "corta" });
        Assert.Equal(HttpStatusCode.BadRequest, debil.StatusCode);

        var repetida = await cliente.PostAsJsonAsync($"{Autenticacion}/cambiar-password", new { passwordActual = "Temporal.2026!x", passwordNueva = "Temporal.2026!x" });
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);

        var cambio = await cliente.PostAsJsonAsync($"{Autenticacion}/cambiar-password", new { passwordActual = "Temporal.2026!x", passwordNueva = "Definitiva.2026!x" });
        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);

        // Ya no lo frena el cambio pendiente; como no tiene roles, lo que ahora le falta es el permiso.
        var despues = await api.IniciarSesionAsync(nombre, "Definitiva.2026!x");
        var sesion = await despues.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/sesion");
        Assert.False(sesion.GetProperty("debeCambiarPassword").GetBoolean());
        Assert.Equal("PERMISO_DENEGADO", await (await despues.GetAsync(Sucursales)).ErrorCodeAsync());
    }

    [Fact]
    public async Task Sin_el_permiso_responde_403_aunque_se_llame_a_la_API_directamente()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(Sucursales)).StatusCode);

        var crear = await cliente.PostAsJsonAsync(Sucursales, new { codigo = "X1", nombre = "No debería crearse" });
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        Assert.Equal("PERMISO_DENEGADO", await crear.ErrorCodeAsync());

        var usuarios = await cliente.GetAsync("/api/v1/seguridad/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, usuarios.StatusCode);
    }

    [Fact]
    public async Task El_menu_y_los_permisos_reflejan_solo_lo_que_el_usuario_puede_hacer()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");

        var permisos = await cliente.GetFromJsonAsync<string[]>("/api/v1/seguridad/mis-permisos");
        Assert.Equal(["ORGANIZACION_SUCURSALES_VER"], permisos!);

        var menu = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/menu");
        var modulo = Assert.Single(menu.EnumerateArray());
        Assert.Equal("MNU_ORGANIZACION", modulo.GetProperty("codigo").GetString());
        var pantalla = Assert.Single(modulo.GetProperty("hijos").EnumerateArray());
        Assert.Equal("/organizacion/sucursales", pantalla.GetProperty("ruta").GetString());
    }

    [Fact]
    public async Task La_denegacion_directa_gana_al_permiso_del_rol()
    {
        var (_, usuarioId, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var admin = await api.ClienteAdminAsync();
        var permisoId = await api.EscalarAsync<long>("SELECT id FROM permiso WHERE codigo = 'ORGANIZACION_SUCURSALES_VER'");

        var guardar = await admin.PutAsJsonAsync($"/api/v1/seguridad/usuarios/{usuarioId}/permisos",
            new { permisos = new[] { new { permisoId, efecto = "DENEGAR", vigenteHasta = (DateTime?)null, motivo = "Prueba" } } });
        Assert.Equal(HttpStatusCode.OK, guardar.StatusCode);

        // Surte efecto de inmediato, con el mismo token de acceso.
        var respuesta = await cliente.GetAsync(Sucursales);
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("PERMISO_DENEGADO", await respuesta.ErrorCodeAsync());
    }

    [Fact]
    public async Task Un_modulo_no_liberado_responde_403_y_desaparece_del_menu()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("AUDITORIA_BITACORA_VER");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/auditoria/bitacora")).StatusCode);

        // Auditoría es módulo base y la aplicación no deja suspenderlo; se fuerza en la base para probar el control.
        await api.EjecutarAsync("UPDATE modulo SET estado_global = 'SUSPENDIDO' WHERE codigo = 'AUDITORIA'");
        await api.EjecutarAsync("UPDATE seguridad_version SET version = version + 1 WHERE id = 1");
        try
        {
            var respuesta = await cliente.GetAsync("/api/v1/auditoria/bitacora");
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Equal("MODULO_NO_DISPONIBLE", await respuesta.ErrorCodeAsync());
            Assert.Empty((await cliente.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/menu")).EnumerateArray());
            Assert.Empty((await cliente.GetFromJsonAsync<string[]>("/api/v1/seguridad/mis-permisos"))!);
        }
        finally
        {
            await api.EjecutarAsync("UPDATE modulo SET estado_global = 'LIBERADO' WHERE codigo = 'AUDITORIA'");
            await api.EjecutarAsync("UPDATE seguridad_version SET version = version + 1 WHERE id = 1");
        }

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/auditoria/bitacora")).StatusCode);
    }

    [Fact]
    public async Task Cambiar_el_estado_de_un_modulo_exige_motivo_y_queda_auditado()
    {
        var admin = await api.ClienteAdminAsync();
        var modulos = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/modulos");
        // Catálogos pasó a LIBERADO y base en la 1A; se usa un módulo aún en desarrollo.
        var catalogos = modulos.EnumerateArray().Single(m => m.GetProperty("codigo").GetString() == "CLIENTES");
        var seguridad = modulos.EnumerateArray().Single(m => m.GetProperty("codigo").GetString() == "SEGURIDAD");
        var id = catalogos.GetProperty("id").GetInt64();

        var sinMotivo = await admin.PutAsJsonAsync($"/api/v1/seguridad/modulos/{id}/estado",
            new { ambito = "GLOBAL", estado = "EN_PRUEBAS", motivo = "", versionFila = catalogos.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var cambio = await admin.PutAsJsonAsync($"/api/v1/seguridad/modulos/{id}/estado",
            new { ambito = "GLOBAL", estado = "EN_PRUEBAS", motivo = "Inicio de pruebas", versionFila = catalogos.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        // La empresa sigue EN_DESARROLLO, que es más restrictivo.
        Assert.Equal("EN_DESARROLLO", (await cambio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estadoEfectivo").GetString());

        var concurrente = await admin.PutAsJsonAsync($"/api/v1/seguridad/modulos/{id}/estado",
            new { ambito = "GLOBAL", estado = "LIBERADO", motivo = "Versión vieja", versionFila = catalogos.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.Conflict, concurrente.StatusCode);

        var moduloBase = await admin.PutAsJsonAsync($"/api/v1/seguridad/modulos/{seguridad.GetProperty("id").GetInt64()}/estado",
            new { ambito = "GLOBAL", estado = "SUSPENDIDO", motivo = "No debería", versionFila = seguridad.GetProperty("versionFila").GetUInt32() });
        Assert.Equal((HttpStatusCode)422, moduloBase.StatusCode);

        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria WHERE entidad = 'modulo' AND entidad_id = @id AND accion = 'CAMBIAR_ESTADO' AND mensaje LIKE '%Inicio de pruebas%'",
            ("@id", id.ToString())));
    }

    [Fact]
    public async Task La_cuenta_se_bloquea_tras_los_intentos_fallidos_configurados()
    {
        var (usuario, _, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var cliente = api.ClienteAnonimo();

        for (var i = 0; i < 5; i++)
        {
            var fallo = await cliente.PostAsJsonAsync($"{Autenticacion}/login", new { nombreUsuario = usuario, password = "Incorrecta.123!" });
            Assert.Equal("CREDENCIALES_INVALIDAS", await fallo.ErrorCodeAsync());
        }

        var conLaCorrecta = await cliente.PostAsJsonAsync($"{Autenticacion}/login", new { nombreUsuario = usuario, password = "Definitiva.2026!x" });
        Assert.Equal(HttpStatusCode.Unauthorized, conLaCorrecta.StatusCode);
        Assert.Equal("CUENTA_BLOQUEADA", await conLaCorrecta.ErrorCodeAsync());

        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria_acceso WHERE evento = 'BLOQUEO' AND nombre_usuario = @u", ("@u", usuario)));
        Assert.Equal(6, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM intento_login WHERE nombre_usuario = @u AND exitoso = 0", ("@u", usuario)));
    }

    [Fact]
    public async Task El_refresh_token_rota_y_su_reutilizacion_cierra_la_sesion_completa()
    {
        var (usuario, _, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var cliente = api.ClienteAnonimo();
        var primero = (await api.LoginAsync(usuario, "Definitiva.2026!x")).GetProperty("refreshToken").GetString();

        var rotacion = await cliente.PostAsJsonAsync($"{Autenticacion}/refresh", new { refreshToken = primero });
        Assert.Equal(HttpStatusCode.OK, rotacion.StatusCode);
        var segundo = (await rotacion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        Assert.NotEqual(primero, segundo);

        // El primer token ya fue usado: presentarlo otra vez es señal de robo.
        var reutilizado = await cliente.PostAsJsonAsync($"{Autenticacion}/refresh", new { refreshToken = primero });
        Assert.Equal(HttpStatusCode.Unauthorized, reutilizado.StatusCode);

        var elNuevoTampoco = await cliente.PostAsJsonAsync($"{Autenticacion}/refresh", new { refreshToken = segundo });
        Assert.Equal(HttpStatusCode.Unauthorized, elNuevoTampoco.StatusCode);

        // Dos eventos: el token ya rotado y el nuevo, que quedó revocado junto con toda la sesión.
        Assert.Equal(2, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria_acceso a JOIN usuario u ON u.id = a.usuario_id WHERE a.evento = 'REFRESH_REUTILIZADO' AND u.nombre_usuario = @u",
            ("@u", usuario)));
    }

    [Fact]
    public async Task El_refresh_token_se_guarda_como_hash_y_el_logout_lo_revoca()
    {
        var (usuario, _, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var cliente = api.ClienteAnonimo();
        var refresh = (await api.LoginAsync(usuario, "Definitiva.2026!x")).GetProperty("refreshToken").GetString();

        Assert.Equal(0, await api.EscalarAsync<int>("SELECT COUNT(*) FROM refresh_token WHERE token_hash = @t", ("@t", refresh)));

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync($"{Autenticacion}/logout", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync($"{Autenticacion}/refresh", new { refreshToken = refresh })).StatusCode);
    }

    [Fact]
    public async Task Desactivar_un_usuario_invalida_su_token_de_inmediato()
    {
        var (_, usuarioId, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var admin = await api.ClienteAdminAsync();
        var detalle = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/seguridad/usuarios/{usuarioId}");

        var baja = await admin.DeleteAsync($"/api/v1/seguridad/usuarios/{usuarioId}?versionFila={detalle.GetProperty("versionFila").GetUInt32()}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(Sucursales)).StatusCode);
    }

    [Fact]
    public async Task No_se_puede_dejar_el_sistema_sin_administrador_ni_modificar_el_rol_de_sistema()
    {
        var admin = await api.ClienteAdminAsync();
        var yo = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/usuarios/1");
        var version = yo.GetProperty("versionFila").GetUInt32();

        var autoBaja = await admin.DeleteAsync($"/api/v1/seguridad/usuarios/1?versionFila={version}");
        Assert.Equal((HttpStatusCode)422, autoBaja.StatusCode);

        var sinRol = await admin.PutAsJsonAsync("/api/v1/seguridad/usuarios/1", new
        {
            correo = "admin@erpgt.local", nombreCompleto = "Administrador del sistema", activo = true,
            rolIds = Array.Empty<long>(), empresaIds = new[] { 1L }, sucursalIds = new[] { 1L }, versionFila = version,
        });
        Assert.Equal((HttpStatusCode)422, sinRol.StatusCode);
        Assert.Equal("SIN_ADMINISTRADOR", await sinRol.ErrorCodeAsync());

        var rol = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/roles/1");
        var editarRol = await admin.PutAsJsonAsync("/api/v1/seguridad/roles/1", new
        {
            nombre = "Administrador", descripcion = (string?)null, activo = true, permisoIds = Array.Empty<long>(),
            versionFila = rol.GetProperty("versionFila").GetUInt32(),
        });
        Assert.Equal((HttpStatusCode)422, editarRol.StatusCode);
        Assert.Equal("ROL_SISTEMA", await editarRol.ErrorCodeAsync());
    }

    [Fact]
    public async Task La_recuperacion_de_contrasena_responde_igual_siempre_y_el_enlace_sirve_una_sola_vez()
    {
        var (usuario, _, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var cliente = api.ClienteAnonimo();

        var inexistente = await cliente.PostAsJsonAsync($"{Autenticacion}/recuperacion", new { usuario = "no-existe" });
        var existente = await cliente.PostAsJsonAsync($"{Autenticacion}/recuperacion", new { usuario });
        Assert.Equal(HttpStatusCode.Accepted, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, existente.StatusCode);

        var correo = Directory.GetFiles(api.CarpetaCorreo).Select(File.ReadAllText).Single(c => c.Contains($"{usuario}@pruebas.local"));
        var token = Uri.UnescapeDataString(correo.Split("token=")[1].Split('\n', ' ')[0].Trim());

        var restablecer = await cliente.PostAsJsonAsync($"{Autenticacion}/restablecer", new { token, passwordNueva = "Recuperada.2026!x" });
        Assert.Equal(HttpStatusCode.NoContent, restablecer.StatusCode);

        var segundoUso = await cliente.PostAsJsonAsync($"{Autenticacion}/restablecer", new { token, passwordNueva = "Otra.Distinta.2026!x" });
        Assert.Equal((HttpStatusCode)422, segundoUso.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await (await api.IniciarSesionAsync(usuario, "Recuperada.2026!x")).GetAsync(Sucursales)).StatusCode);
    }

    [Fact]
    public async Task No_se_puede_cambiar_a_una_empresa_o_sucursal_sin_acceso()
    {
        var (usuario, _, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_SUCURSALES_VER");
        var refresh = (await api.LoginAsync(usuario, "Definitiva.2026!x")).GetProperty("refreshToken").GetString();

        var respuesta = await api.ClienteAnonimo().PostAsJsonAsync($"{Autenticacion}/contexto",
            new { refreshToken = refresh, empresaId = 999, sucursalId = 999 });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("CONTEXTO_INVALIDO", await respuesta.ErrorCodeAsync());
    }
}
