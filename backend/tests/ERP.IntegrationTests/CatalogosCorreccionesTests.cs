using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Application.Catalogos;
using ERP.Domain.Catalogos;
using ERP.Domain.Comun;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

// Pruebas de las correcciones de la revisión adversarial de la etapa 1A: aislamiento entre empresas,
// catálogos globales solo para el administrador, dependientes al desactivar, alcance de las series,
// período bloqueado en tipos de cambio e impuestos y escrituras simultáneas de configuración.

/// <summary>Datos de una segunda empresa para probar que nada cruza de una a otra.</summary>
internal static class OtraEmpresa
{
    public const long Id = 902;
    public const long SucursalId = 902;
    public const long BodegaId = 902;

    public static async Task AsegurarAsync(ApiFixture api)
    {
        await api.EjecutarAsync(
            "INSERT IGNORE INTO empresa (id, codigo, razon_social, nombre_comercial, nit, moneda_local_id) " +
            "VALUES (902, 'EMP902', 'Otra empresa', 'Otra empresa', 'CF', 1)");
        await api.EjecutarAsync(
            "INSERT IGNORE INTO sucursal (id, empresa_id, codigo, nombre, es_principal) VALUES (902, 902, 'S902', 'Sucursal de la otra', 1)");
        await api.EjecutarAsync(
            "INSERT IGNORE INTO bodega (id, empresa_id, sucursal_id, codigo, nombre) VALUES (902, 902, 902, 'B902', 'Bodega de la otra')");
    }

    /// <summary>Da al usuario acceso a la otra empresa, su sucursal y su bodega, e invalida la caché de permisos.</summary>
    public static async Task DarAccesoAsync(ApiFixture api, long usuarioId)
    {
        await api.EjecutarAsync($"INSERT IGNORE INTO usuario_empresa (usuario_id, empresa_id) VALUES ({usuarioId}, {Id})");
        await api.EjecutarAsync($"INSERT IGNORE INTO usuario_sucursal (usuario_id, sucursal_id) VALUES ({usuarioId}, {SucursalId})");
        await api.EjecutarAsync($"INSERT IGNORE INTO usuario_bodega (usuario_id, bodega_id, es_predeterminada) VALUES ({usuarioId}, {BodegaId}, 1)");
        await InvalidarAutorizacionAsync(api);
    }

    public static Task InvalidarAutorizacionAsync(ApiFixture api) =>
        api.EjecutarAsync("UPDATE seguridad_version SET version = version + 1, modificado_en = UTC_TIMESTAMP(6) WHERE id = 1");
}

/// <summary>La edición de la empresa solo vale para la empresa activa del permiso.</summary>
[Collection(ColeccionApi.Nombre)]
public class EmpresaActivaTests(ApiFixture api)
{
    [Fact]
    public async Task Con_permiso_en_la_empresa_activa_no_se_edita_otra_empresa_a_la_que_se_tiene_acceso()
    {
        await OtraEmpresa.AsegurarAsync(api);
        var (_, usuarioId, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_EMPRESAS_VER", "ORGANIZACION_EMPRESAS_EDITAR");
        await OtraEmpresa.DarAccesoAsync(api, usuarioId);

        var empresas = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/organizacion/empresas");
        Assert.Contains(empresas.EnumerateArray(), e => e.GetProperty("id").GetInt64() == OtraEmpresa.Id);

        var monedaId = await api.EscalarAsync<long>("SELECT id FROM moneda WHERE codigo = 'GTQ'");
        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/organizacion/empresas/{OtraEmpresa.Id}", new
        {
            razonSocial = "Intento de cambio", nombreComercial = "Intento", nit = "1234567-9", regimenTributario = "GENERAL",
            monedaLocalId = monedaId, versionFila = 1,
        });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("EMPRESA_NO_ACTIVA", await respuesta.ErrorCodeAsync());
        Assert.Equal("Otra empresa", await api.EscalarAsync<string>("SELECT razon_social FROM empresa WHERE id = 902"));
    }
}

/// <summary>Las bodegas y cajas de un usuario se administran por empresa.</summary>
[Collection(ColeccionApi.Nombre)]
public class AccesosOperativosPorEmpresaTests(ApiFixture api)
{
    [Fact]
    public async Task Guardar_no_toca_las_asignaciones_de_otras_empresas_ni_acepta_bodegas_ajenas()
    {
        await OtraEmpresa.AsegurarAsync(api);
        var admin = await api.ClienteAdminAsync();
        var (_, usuarioId, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_BODEGAS_VER");
        await OtraEmpresa.DarAccesoAsync(api, usuarioId);
        var ruta = $"/api/v1/seguridad/usuarios/{usuarioId}/bodegas-cajas";

        // Desde la empresa 1 no se ven las asignaciones de la otra empresa.
        var vista = await admin.GetFromJsonAsync<JsonElement>(ruta);
        Assert.Empty(vista.GetProperty("bodegaIds").EnumerateArray());

        // Una bodega de otra empresa no se puede asignar desde esta, aunque el usuario tenga acceso a ella.
        var ajena = await admin.PutAsJsonAsync(ruta, new { bodegaIds = new[] { OtraEmpresa.BodegaId }, cajaIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.BadRequest, ajena.StatusCode);
        Assert.True((await ajena.LeerAsync()).GetProperty("errors").TryGetProperty("bodegaIds", out _));

        // Guardar las propias reemplaza solo las de la empresa activa: la asignación de la otra empresa sobrevive.
        var propia = (await admin.CrearAsync("/api/v1/organizacion/bodegas",
            new { codigo = Ayudas.Codigo("BOD"), nombre = "Bodega propia", sucursalId = 1 })).GetProperty("id").GetInt64();
        var guardada = await admin.PutAsJsonAsync(ruta, new { bodegaIds = new[] { propia }, cajaIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.OK, guardada.StatusCode);
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM usuario_bodega WHERE usuario_id = @u AND bodega_id = 902", ("@u", usuarioId)));

        var vaciada = await admin.PutAsJsonAsync(ruta, new { bodegaIds = Array.Empty<long>(), cajaIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.OK, vaciada.StatusCode);
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM usuario_bodega WHERE usuario_id = @u AND bodega_id = 902", ("@u", usuarioId)));
        Assert.Equal(0, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM usuario_bodega WHERE usuario_id = @u AND bodega_id = @b", ("@u", usuarioId), ("@b", propia)));
    }

    [Fact]
    public async Task Un_usuario_que_no_pertenece_a_la_empresa_activa_no_existe_para_este_endpoint()
    {
        var admin = await api.ClienteAdminAsync();
        var (_, usuarioId, _) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_BODEGAS_VER");
        await api.EjecutarAsync($"DELETE FROM usuario_empresa WHERE usuario_id = {usuarioId} AND empresa_id = 1");
        await OtraEmpresa.InvalidarAutorizacionAsync(api);
        var ruta = $"/api/v1/seguridad/usuarios/{usuarioId}/bodegas-cajas";

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync(ruta, new { bodegaIds = Array.Empty<long>(), cajaIds = Array.Empty<long>() })).StatusCode);
    }
}

/// <summary>Monedas y tipos de documento los comparten todas las empresas: solo el administrador los cambia.</summary>
[Collection(ColeccionApi.Nombre)]
public class CatalogosGlobalesTests(ApiFixture api)
{
    private const string Monedas = "/api/v1/catalogos/monedas";
    private const string TiposDocumento = "/api/v1/catalogos/tipos-documento";

    [Fact]
    public async Task Sin_el_permiso_de_catalogos_globales_se_consulta_pero_no_se_modifica()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("CATALOGOS_MONEDAS_VER", "CATALOGOS_TIPOSDOCUMENTO_VER");
        var gtq = await api.EscalarAsync<long>("SELECT id FROM moneda WHERE codigo = 'GTQ'");
        var cotizacion = await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = 'COTIZACION'");

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(Monedas)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(TiposDocumento)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync(Monedas, new { codigo = "ZZZ", nombre = "No", simbolo = "Z", decimales = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync($"{Monedas}/{gtq}", new { nombre = "Quetzal", simbolo = "Q", decimales = 6, activo = true, versionFila = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.DeleteAsync($"{Monedas}/{gtq}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync($"{TiposDocumento}/{cotizacion}", new { nombre = "Hackeada", activo = true, versionFila = 1 })).StatusCode);
    }

    [Fact]
    public async Task El_permiso_global_es_solo_del_administrador_y_los_permisos_viejos_ya_no_existen()
    {
        Assert.Equal(0, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM permiso WHERE codigo IN ('CATALOGOS_MONEDAS_CREAR', 'CATALOGOS_MONEDAS_EDITAR', " +
            "'CATALOGOS_MONEDAS_ELIMINAR', 'CATALOGOS_TIPOSDOCUMENTO_EDITAR')"));

        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM rol_permiso rp JOIN rol r ON r.id = rp.rol_id JOIN permiso p ON p.id = rp.permiso_id " +
            "WHERE p.codigo = 'CATALOGOS_GLOBALES_EDITAR' AND r.codigo = 'ADMINISTRADOR'"));
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(DISTINCT rp.rol_id) FROM rol_permiso rp JOIN permiso p ON p.id = rp.permiso_id " +
            "WHERE p.codigo = 'CATALOGOS_GLOBALES_EDITAR'"));
    }

    [Fact]
    public async Task El_administrador_si_edita_los_catalogos_globales()
    {
        var admin = await api.ClienteAdminAsync();
        var id = await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = 'COTIZACION'");
        var actual = await admin.GetFromJsonAsync<JsonElement>($"{TiposDocumento}/{id}");

        var respuesta = await admin.PutAsJsonAsync($"{TiposDocumento}/{id}",
            new { nombre = "Cotización", activo = true, versionFila = actual.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}

/// <summary>Un padre con hijos activos no se desactiva.</summary>
[Collection(ColeccionApi.Nombre)]
public class DependientesTests(ApiFixture api)
{
    private const string Unidades = "/api/v1/catalogos/unidades-medida";
    private const string Conversiones = "/api/v1/catalogos/conversiones-unidad";
    private const string Series = "/api/v1/catalogos/series-documento";

    private async Task<long> SerieAsync(HttpClient admin, string tipo, long? sucursalId = null, long? cajaId = null)
    {
        var tipoId = await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = @c", ("@c", tipo));
        return (await admin.CrearAsync(Series, new
        {
            tipoDocumentoId = tipoId, sucursalId, cajaId, serie = Ayudas.Codigo("D", 8), longitud = 6, siguienteCorrelativo = 1,
            vigenteDesde = "2026-01-01",
        })).GetProperty("id").GetInt64();
    }

    private static async Task AssertTieneDependientesAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal((HttpStatusCode)422, respuesta.StatusCode);
        Assert.Equal("TIENE_DEPENDIENTES", await respuesta.ErrorCodeAsync());
    }

    [Fact]
    public async Task Una_unidad_con_conversiones_activas_no_se_desactiva()
    {
        var admin = await api.ClienteAdminAsync();
        var propia = (await admin.CrearAsync(Unidades, new { codigo = Ayudas.Codigo("UD", 8), nombre = "Caja doble", magnitud = "CANTIDAD", decimales = 0 }))
            .GetProperty("id").GetInt64();
        var uni = await api.EscalarAsync<long>("SELECT id FROM unidad_medida WHERE codigo = 'UNI' AND empresa_id IS NULL");
        var conversion = (await admin.CrearAsync(Conversiones, new { unidadOrigenId = propia, unidadDestinoId = uni, factor = 24m }))
            .GetProperty("id").GetInt64();

        await AssertTieneDependientesAsync(await admin.DeleteAsync($"{Unidades}/{propia}?versionFila=1"));

        // Desactivada la conversión, la unidad ya se puede dar de baja.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Conversiones}/{conversion}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Unidades}/{propia}?versionFila=1")).StatusCode);
    }

    [Fact]
    public async Task Un_tipo_de_documento_con_series_activas_no_se_desactiva()
    {
        var admin = await api.ClienteAdminAsync();
        var serie = await SerieAsync(admin, "COTIZACION");
        var tipoId = await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = 'COTIZACION'");
        var tipo = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/catalogos/tipos-documento/{tipoId}");

        await AssertTieneDependientesAsync(await admin.PutAsJsonAsync($"/api/v1/catalogos/tipos-documento/{tipoId}",
            new { nombre = tipo.GetProperty("nombre").GetString(), activo = false, versionFila = tipo.GetProperty("versionFila").GetUInt32() }));

        Assert.True(serie > 0);
    }

    [Fact]
    public async Task Una_caja_o_una_sucursal_con_series_activas_no_se_desactiva()
    {
        var admin = await api.ClienteAdminAsync();
        var sucursalId = (await admin.CrearAsync("/api/v1/organizacion/sucursales",
            new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Sucursal con serie" })).GetProperty("id").GetInt64();
        var cajaId = (await admin.CrearAsync("/api/v1/organizacion/cajas",
            new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja con serie", sucursalId })).GetProperty("id").GetInt64();
        var serieDeCaja = await SerieAsync(admin, "FACTURA_VENTA", sucursalId, cajaId);

        await AssertTieneDependientesAsync(await admin.DeleteAsync($"/api/v1/organizacion/cajas/{cajaId}?versionFila=1"));

        // Cerrada la serie de la caja, la caja baja; la sucursal sigue sin poder bajar mientras tenga series propias.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Series}/{serieDeCaja}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/organizacion/cajas/{cajaId}?versionFila=1")).StatusCode);

        var serieDeSucursal = await SerieAsync(admin, "FACTURA_COMPRA", sucursalId);
        await AssertTieneDependientesAsync(await admin.DeleteAsync($"/api/v1/organizacion/sucursales/{sucursalId}?versionFila=1"));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Series}/{serieDeSucursal}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/organizacion/sucursales/{sucursalId}?versionFila=1")).StatusCode);
    }
}

/// <summary>Una serie solo sirve al tipo, la sucursal y la caja para los que se creó.</summary>
[Collection(ColeccionApi.Nombre)]
public class AlcanceDeSerieTests(ApiFixture api)
{
    private const string Series = "/api/v1/catalogos/series-documento";
    private static readonly DateOnly Fecha = new(2026, 10, 10);

    private async Task<long> TipoAsync(string codigo) =>
        await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = @c", ("@c", codigo));

    private async Task<long> SiguienteGuardadoAsync(long serieId) =>
        await api.EscalarAsync<long>("SELECT siguiente_correlativo FROM serie_documento WHERE id = @i", ("@i", serieId));

    private async Task<NumeroAsignado> AsignarAsync(long serieId, AlcanceDocumento alcance)
    {
        await using var ambito = api.Servicios.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<INumeracion>().SiguienteAsync(1, serieId, alcance, Fecha, CancellationToken.None);
    }

    private async Task<string> RechazoAsync(long serieId, AlcanceDocumento alcance)
    {
        await using var ambito = api.Servicios.CreateAsyncScope();
        var error = await Assert.ThrowsAnyAsync<ErpException>(() =>
            ambito.ServiceProvider.GetRequiredService<INumeracion>().SiguienteAsync(1, serieId, alcance, Fecha, CancellationToken.None));
        return error.ErrorCode;
    }

    [Fact]
    public async Task Una_serie_de_otro_tipo_sucursal_o_caja_se_rechaza_sin_consumir_numero()
    {
        var admin = await api.ClienteAdminAsync();
        var facturaVenta = await TipoAsync("FACTURA_VENTA");
        var cotizacion = await TipoAsync("COTIZACION");

        var sucursalB = (await admin.CrearAsync("/api/v1/organizacion/sucursales",
            new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Otra sucursal" })).GetProperty("id").GetInt64();
        var cajaId = (await admin.CrearAsync("/api/v1/organizacion/cajas",
            new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja 1", sucursalId = 1 })).GetProperty("id").GetInt64();
        var otraCaja = (await admin.CrearAsync("/api/v1/organizacion/cajas",
            new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja 2", sucursalId = 1 })).GetProperty("id").GetInt64();

        object Cuerpo(long? sucursal, long? caja) => new
        {
            tipoDocumentoId = facturaVenta, sucursalId = sucursal, cajaId = caja, serie = Ayudas.Codigo("A", 8), longitud = 6,
            siguienteCorrelativo = 1, vigenteDesde = "2026-01-01",
        };

        var deEmpresa = (await admin.CrearAsync(Series, Cuerpo(null, null))).GetProperty("id").GetInt64();
        var deSucursal = (await admin.CrearAsync(Series, Cuerpo(1, null))).GetProperty("id").GetInt64();
        var deCaja = (await admin.CrearAsync(Series, Cuerpo(1, cajaId))).GetProperty("id").GetInt64();

        // Otro tipo de documento: rechazado aunque se conozca el id de la serie.
        Assert.Equal("SERIE_TIPO_INCORRECTO", await RechazoAsync(deEmpresa, new AlcanceDocumento(cotizacion, 1, null)));
        Assert.Equal(1, await SiguienteGuardadoAsync(deEmpresa));

        // La serie de toda la empresa sirve en cualquier sucursal y caja.
        Assert.Equal(1, (await AsignarAsync(deEmpresa, new AlcanceDocumento(facturaVenta, sucursalB, cajaId))).Numero);

        // La serie de una sucursal no sirve en otra.
        Assert.Equal("SERIE_FUERA_DE_ALCANCE", await RechazoAsync(deSucursal, new AlcanceDocumento(facturaVenta, sucursalB, null)));
        Assert.Equal(1, await SiguienteGuardadoAsync(deSucursal));
        Assert.Equal(1, (await AsignarAsync(deSucursal, new AlcanceDocumento(facturaVenta, 1, null))).Numero);

        // La serie de una caja exige esa caja: ni sin caja ni con otra.
        Assert.Equal("SERIE_FUERA_DE_ALCANCE", await RechazoAsync(deCaja, new AlcanceDocumento(facturaVenta, 1, null)));
        Assert.Equal("SERIE_FUERA_DE_ALCANCE", await RechazoAsync(deCaja, new AlcanceDocumento(facturaVenta, 1, otraCaja)));
        Assert.Equal(1, await SiguienteGuardadoAsync(deCaja));
        Assert.Equal(1, (await AsignarAsync(deCaja, new AlcanceDocumento(facturaVenta, 1, cajaId))).Numero);
        Assert.Equal(2, await SiguienteGuardadoAsync(deCaja));
    }
}

/// <summary>El período bloqueado también protege los tipos de cambio y los impuestos.</summary>
[Collection(ColeccionApi.Nombre)]
public class BloqueoDePeriodoEnMaestrosTests(ApiFixture api)
{
    private const string Config = "/api/v1/catalogos/configuracion";
    private const string TiposCambio = "/api/v1/catalogos/tipos-cambio";
    private const string Impuestos = "/api/v1/catalogos/impuestos";

    private static string F(DateOnly fecha) => fecha.ToString("yyyy-MM-dd");

    private static async Task AssertBloqueadoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal((HttpStatusCode)422, respuesta.StatusCode);
        Assert.Equal("PERIODO_BLOQUEADO", await respuesta.ErrorCodeAsync());
    }

    [Fact]
    public async Task No_se_crean_ni_corrigen_tasas_ni_vigencias_de_impuesto_en_un_periodo_cerrado()
    {
        var admin = await api.ClienteAdminAsync();
        var hoy = FechaNegocio.Hoy(DateTime.UtcNow);
        var usd = await api.EscalarAsync<long>("SELECT id FROM moneda WHERE codigo = 'USD'");
        var codigo = Ayudas.Codigo("BQ", 10);

        // Antes de cerrar el período: una tasa de hace 20 días y un impuesto vigente desde hace 40.
        var tasa = await admin.CrearAsync(TiposCambio, new { monedaId = usd, fecha = F(hoy.AddDays(-20)), tasa = 7.7m });
        var tasaId = tasa.GetProperty("id").GetInt64();
        var impuesto = await admin.CrearAsync(Impuestos,
            new { codigo, nombre = "Impuesto con bloqueo", tipo = "OTRO", porcentaje = 3m, vigenteDesde = F(hoy.AddDays(-40)) });
        var impuestoId = impuesto.GetProperty("id").GetInt64();

        try
        {
            var fijada = await admin.PutAsJsonAsync($"{Config}/fecha-bloqueo", new { fecha = F(hoy.AddDays(-10)) });
            Assert.Equal(HttpStatusCode.OK, fijada.StatusCode);

            // ----- Tipos de cambio -----
            await AssertBloqueadoAsync(await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = F(hoy.AddDays(-30)), tasa = 7.6m }));
            await AssertBloqueadoAsync(await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = F(hoy.AddDays(-10)), tasa = 7.6m }));
            await AssertBloqueadoAsync(await admin.PutAsJsonAsync($"{TiposCambio}/{tasaId}",
                new { tasa = 7.9m, versionFila = tasa.GetProperty("versionFila").GetUInt32() }));
            Assert.Equal(HttpStatusCode.Created,
                (await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = F(hoy.AddDays(-5)), tasa = 7.65m })).StatusCode);

            // ----- Impuestos -----
            // Una vigencia que empieza en el período cerrado.
            await AssertBloqueadoAsync(await admin.PostAsJsonAsync(Impuestos,
                new { codigo = Ayudas.Codigo("BQ", 10), nombre = "Retroactivo", tipo = "OTRO", porcentaje = 1m, vigenteDesde = F(hoy.AddDays(-30)) }));

            // Cerrar la vigencia dentro del período cerrado cambiaría el impuesto de días ya cerrados.
            await AssertBloqueadoAsync(await admin.PutAsJsonAsync($"{Impuestos}/{impuestoId}",
                new { nombre = "Impuesto con bloqueo", vigenteHasta = F(hoy.AddDays(-20)), activo = true, versionFila = 1 }));

            // Desactivarlo también alcanza los días cerrados.
            await AssertBloqueadoAsync(await admin.DeleteAsync($"{Impuestos}/{impuestoId}?versionFila=1"));

            // Cambiar solo el nombre no toca ningún día: se permite.
            var renombrado = await admin.PutAsJsonAsync($"{Impuestos}/{impuestoId}",
                new { nombre = "Nombre nuevo", vigenteHasta = (string?)null, activo = true, versionFila = 1 });
            Assert.Equal(HttpStatusCode.OK, renombrado.StatusCode);

            // Cerrar la vigencia después del bloqueo solo afecta días abiertos: se permite.
            var cierreAbierto = await admin.PutAsJsonAsync($"{Impuestos}/{impuestoId}",
                new { nombre = "Nombre nuevo", vigenteHasta = F(hoy.AddDays(-2)), activo = true, versionFila = 2 });
            Assert.Equal(HttpStatusCode.OK, cierreAbierto.StatusCode);

            // Una vigencia nueva que empieza después del bloqueo también se permite.
            var nueva = await admin.PostAsJsonAsync(Impuestos,
                new { codigo, nombre = "Nueva vigencia", tipo = "OTRO", porcentaje = 4m, vigenteDesde = F(hoy.AddDays(-1)) });
            Assert.Equal(HttpStatusCode.Created, nueva.StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{Config}/fecha-bloqueo", new { fecha = (string?)null })).StatusCode);
        }

        // Sin bloqueo, la misma corrección vuelve a ser posible.
        var corregida = await admin.PutAsJsonAsync($"{TiposCambio}/{tasaId}",
            new { tasa = 7.9m, versionFila = tasa.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, corregida.StatusCode);
    }
}

/// <summary>Dos escrituras simultáneas de un parámetro no deben fallar ni duplicar la fila del día.</summary>
[Collection(ColeccionApi.Nombre)]
public class ConfiguracionConcurrenteTests(ApiFixture api)
{
    [Fact]
    public async Task Ocho_cambios_simultaneos_del_mismo_parametro_terminan_bien_y_dejan_una_sola_fila_del_dia()
    {
        await api.EjecutarAsync("DELETE FROM configuracion WHERE empresa_id = 1 AND clave = 'REDONDEO_IMPUESTO'");
        var clientes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => api.ClienteAdminAsync()));

        var respuestas = await Task.WhenAll(clientes.Select((c, i) =>
            c.PutAsJsonAsync("/api/v1/catalogos/configuracion/REDONDEO_IMPUESTO", new { valor = i % 2 == 0 ? "LINEA" : "DOCUMENTO" })));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM configuracion WHERE empresa_id = 1 AND clave = 'REDONDEO_IMPUESTO' AND activo = 1 AND vigente_hasta IS NULL"));
    }
}

/// <summary>Los endpoints de 1A respetan el estado del módulo: suspendido, responden MODULO_NO_DISPONIBLE.</summary>
[Collection(ColeccionApi.Nombre)]
public class ModuloDeshabilitadoTests(ApiFixture api)
{
    [Fact]
    public async Task Con_el_modulo_catalogos_suspendido_sus_endpoints_responden_403_MODULO_NO_DISPONIBLE()
    {
        var admin = await api.ClienteAdminAsync();
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("CATALOGOS_MARCAS_VER", "CATALOGOS_IMPUESTOS_VER", "CATALOGOS_CONFIGURACION_VER");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/catalogos/marcas")).StatusCode);

        try
        {
            // El estado de un módulo base no se cambia por la API; aquí se simula una suspensión directa.
            await api.EjecutarAsync("UPDATE modulo SET estado_global = 'SUSPENDIDO' WHERE codigo = 'CATALOGOS'");
            await OtraEmpresa.InvalidarAutorizacionAsync(api);

            foreach (var ruta in new[] { "/api/v1/catalogos/marcas", "/api/v1/catalogos/impuestos", "/api/v1/catalogos/configuracion" })
            {
                var respuesta = await cliente.GetAsync(ruta);
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
                Assert.Equal("MODULO_NO_DISPONIBLE", await respuesta.ErrorCodeAsync());
            }

            // Ni siquiera el administrador accede a un módulo suspendido.
            Assert.Equal("MODULO_NO_DISPONIBLE", await (await admin.GetAsync("/api/v1/catalogos/monedas")).ErrorCodeAsync());
        }
        finally
        {
            await api.EjecutarAsync("UPDATE modulo SET estado_global = 'LIBERADO' WHERE codigo = 'CATALOGOS'");
            await OtraEmpresa.InvalidarAutorizacionAsync(api);
        }

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/catalogos/marcas")).StatusCode);
    }
}
