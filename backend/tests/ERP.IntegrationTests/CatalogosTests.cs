using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Utilidades compartidas por las pruebas de la etapa 1A.</summary>
internal static class Ayudas
{
    public static string Codigo(string prefijo = "T", int largo = 12) =>
        $"{prefijo}{Guid.NewGuid():N}"[..largo].ToUpperInvariant();

    public static async Task<JsonElement> LeerAsync(this HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<JsonElement> CrearAsync(this HttpClient cliente, string ruta, object cuerpo)
    {
        var respuesta = await cliente.PostAsJsonAsync(ruta, cuerpo);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, $"POST {ruta} respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        return await respuesta.LeerAsync();
    }
}

/// <summary>Empresa: edición de datos, NIT y moneda local.</summary>
[Collection(ColeccionApi.Nombre)]
public class EmpresaTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/organizacion/empresas";

    [Fact]
    public async Task Editar_la_empresa_valida_el_NIT_guarda_y_audita()
    {
        var admin = await api.ClienteAdminAsync();
        var actual = (await admin.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray().Single(e => e.GetProperty("id").GetInt64() == 1);

        object Cuerpo(string nit, uint version, string nombre = "Comercial Pruebas") => new
        {
            razonSocial = "Razón Social de Pruebas, S. A.", nombreComercial = nombre, nit, direccion = "Zona 10", telefono = "2222-3333",
            correo = "info@pruebas.local", regimenTributario = "GENERAL", monedaLocalId = actual.GetProperty("monedaLocalId").GetInt64(),
            versionFila = version,
        };

        var version = actual.GetProperty("versionFila").GetUInt32();

        var invalido = await admin.PutAsJsonAsync($"{Ruta}/1", Cuerpo("1234567-1", version));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.True((await invalido.LeerAsync()).GetProperty("errors").TryGetProperty("nit", out _));

        var guardada = await admin.PutAsJsonAsync($"{Ruta}/1", Cuerpo("1234567-9", version));
        Assert.Equal(HttpStatusCode.OK, guardada.StatusCode);
        var dto = await guardada.LeerAsync();
        Assert.Equal("12345679", dto.GetProperty("nit").GetString());
        Assert.Equal(version + 1, dto.GetProperty("versionFila").GetUInt32());

        // Segunda edición con la versión vieja: conflicto.
        var vieja = await admin.PutAsJsonAsync($"{Ruta}/1", Cuerpo("1234567-9", version, "Otro nombre"));
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
        Assert.Equal("CONCURRENCIA", await vieja.ErrorCodeAsync());

        Assert.True(await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria WHERE entidad = 'empresa' AND entidad_id = '1' AND accion = 'EDITAR' AND empresa_id = 1") >= 1);
    }

    [Fact]
    public async Task Una_empresa_inexistente_o_sin_acceso_responde_404_y_el_regimen_invalido_400()
    {
        var admin = await api.ClienteAdminAsync();
        var actual = (await admin.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray().First();
        object Cuerpo(string regimen) => new
        {
            razonSocial = "X", nombreComercial = "X", nit = "1234567-9", regimenTributario = regimen,
            monedaLocalId = actual.GetProperty("monedaLocalId").GetInt64(), versionFila = 1,
        };

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/999999", Cuerpo("GENERAL"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{Ruta}/1", Cuerpo("INEXISTENTE"))).StatusCode);
    }

    [Fact]
    public async Task Sin_el_permiso_de_edicion_responde_403()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_EMPRESAS_VER");
        var respuesta = await cliente.PutAsJsonAsync($"{Ruta}/1", new { razonSocial = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}

/// <summary>Bodegas, cajas, departamentos y centros de costo: estructura de la empresa.</summary>
[Collection(ColeccionApi.Nombre)]
public class EstructuraTests(ApiFixture api)
{
    private const string Bodegas = "/api/v1/organizacion/bodegas";
    private const string Cajas = "/api/v1/organizacion/cajas";
    private const string Departamentos = "/api/v1/organizacion/departamentos";
    private const string Centros = "/api/v1/organizacion/centros-costo";
    private const string Sucursales = "/api/v1/organizacion/sucursales";

    [Fact]
    public async Task Bodega_crear_editar_baja_y_auditoria()
    {
        var admin = await api.ClienteAdminAsync();
        var codigo = Ayudas.Codigo("BOD");

        var creada = await admin.CrearAsync(Bodegas, new { codigo = codigo.ToLowerInvariant(), nombre = "Bodega central", sucursalId = 1, descripcion = "  Principal " });
        var id = creada.GetProperty("id").GetInt64();
        Assert.Equal(codigo, creada.GetProperty("codigo").GetString());
        Assert.Equal("Principal", creada.GetProperty("descripcion").GetString());
        Assert.Equal(1, creada.GetProperty("empresaId").GetInt64());
        Assert.False(string.IsNullOrEmpty(creada.GetProperty("sucursalNombre").GetString()));

        var editada = await admin.PutAsJsonAsync($"{Bodegas}/{id}", new { nombre = "Bodega editada", descripcion = (string?)null, activo = true, versionFila = 1 });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        Assert.Equal(2u, (await editada.LeerAsync()).GetProperty("versionFila").GetUInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Bodegas}/{id}?versionFila=2")).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<JsonElement>($"{Bodegas}/{id}")).GetProperty("activo").GetBoolean());

        foreach (var accion in new[] { "CREAR", "EDITAR", "ELIMINAR" })
            Assert.Equal(1, await api.EscalarAsync<int>(
                "SELECT COUNT(*) FROM auditoria WHERE entidad = 'bodega' AND entidad_id = @id AND accion = @a AND usuario_id = 1 AND operacion_id IS NOT NULL",
                ("@id", id.ToString()), ("@a", accion)));
    }

    [Fact]
    public async Task Un_codigo_repetido_o_una_version_vieja_responden_409()
    {
        var admin = await api.ClienteAdminAsync();
        var codigo = Ayudas.Codigo("BOD");
        var id = (await admin.CrearAsync(Bodegas, new { codigo, nombre = "Una", sucursalId = 1 })).GetProperty("id").GetInt64();

        var repetida = await admin.PostAsJsonAsync(Bodegas, new { codigo, nombre = "Otra", sucursalId = 1 });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("DUPLICADO", await repetida.ErrorCodeAsync());

        var cuerpo = new { nombre = "Editada", descripcion = (string?)null, activo = true, versionFila = 1 };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{Bodegas}/{id}", cuerpo)).StatusCode);
        var concurrente = await admin.PutAsJsonAsync($"{Bodegas}/{id}", cuerpo);
        Assert.Equal(HttpStatusCode.Conflict, concurrente.StatusCode);
        Assert.Equal("CONCURRENCIA", await concurrente.ErrorCodeAsync());
    }

    [Fact]
    public async Task La_bodega_exige_una_sucursal_activa_de_la_empresa()
    {
        var admin = await api.ClienteAdminAsync();

        var inexistente = await admin.PostAsJsonAsync(Bodegas, new { codigo = Ayudas.Codigo("BOD"), nombre = "X", sucursalId = 999999 });
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.True((await inexistente.LeerAsync()).GetProperty("errors").TryGetProperty("sucursalId", out _));

        // Sucursal inactiva
        var sucursal = await admin.CrearAsync(Sucursales, new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Sucursal a dar de baja" });
        var sucursalId = sucursal.GetProperty("id").GetInt64();
        await admin.DeleteAsync($"{Sucursales}/{sucursalId}?versionFila=1");
        var contraInactiva = await admin.PostAsJsonAsync(Bodegas, new { codigo = Ayudas.Codigo("BOD"), nombre = "X", sucursalId });
        Assert.Equal(HttpStatusCode.BadRequest, contraInactiva.StatusCode);

        // Una sucursal de OTRA empresa tampoco sirve.
        await api.EjecutarAsync(
            "INSERT IGNORE INTO empresa (id, codigo, razon_social, nombre_comercial, nit, moneda_local_id) VALUES (900, 'EMP900', 'Otra', 'Otra', 'CF', 1)");
        await api.EjecutarAsync("INSERT IGNORE INTO sucursal (id, empresa_id, codigo, nombre) VALUES (900, 900, 'S900', 'Ajena')");
        var ajena = await admin.PostAsJsonAsync(Bodegas, new { codigo = Ayudas.Codigo("BOD"), nombre = "X", sucursalId = 900 });
        Assert.Equal(HttpStatusCode.BadRequest, ajena.StatusCode);
    }

    [Fact]
    public async Task No_se_desactiva_una_bodega_con_cajas_ni_una_sucursal_con_bodegas_activas()
    {
        var admin = await api.ClienteAdminAsync();
        var sucursalId = (await admin.CrearAsync(Sucursales, new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Sucursal con bodega" })).GetProperty("id").GetInt64();
        var bodegaId = (await admin.CrearAsync(Bodegas, new { codigo = Ayudas.Codigo("BOD"), nombre = "Bodega", sucursalId })).GetProperty("id").GetInt64();
        var cajaId = (await admin.CrearAsync(Cajas, new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja", sucursalId, bodegaId })).GetProperty("id").GetInt64();

        var sucursal = await admin.DeleteAsync($"{Sucursales}/{sucursalId}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, sucursal.StatusCode);
        Assert.Equal("TIENE_DEPENDIENTES", await sucursal.ErrorCodeAsync());

        var bodega = await admin.DeleteAsync($"{Bodegas}/{bodegaId}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, bodega.StatusCode);
        Assert.Equal("TIENE_DEPENDIENTES", await bodega.ErrorCodeAsync());

        // En el orden correcto sí se puede.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Cajas}/{cajaId}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Bodegas}/{bodegaId}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Sucursales}/{sucursalId}?versionFila=1")).StatusCode);
    }

    [Fact]
    public async Task La_bodega_de_una_caja_debe_ser_de_su_misma_sucursal()
    {
        var admin = await api.ClienteAdminAsync();
        var otra = (await admin.CrearAsync(Sucursales, new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Otra sucursal" })).GetProperty("id").GetInt64();
        var bodegaDeOtra = (await admin.CrearAsync(Bodegas, new { codigo = Ayudas.Codigo("BOD"), nombre = "Bodega ajena", sucursalId = otra })).GetProperty("id").GetInt64();

        var respuesta = await admin.PostAsJsonAsync(Cajas, new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja", sucursalId = 1, bodegaId = bodegaDeOtra });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await respuesta.LeerAsync()).GetProperty("errors").TryGetProperty("bodegaId", out _));
    }

    [Fact]
    public async Task Departamento_y_centro_de_costo_con_dependencia()
    {
        var admin = await api.ClienteAdminAsync();
        var departamentoId = (await admin.CrearAsync(Departamentos, new { codigo = Ayudas.Codigo("DEP"), nombre = "Ventas" })).GetProperty("id").GetInt64();
        var centroId = (await admin.CrearAsync(Centros, new { codigo = Ayudas.Codigo("CC"), nombre = "Tienda 1", departamentoId })).GetProperty("id").GetInt64();

        var centro = await admin.GetFromJsonAsync<JsonElement>($"{Centros}/{centroId}");
        Assert.Equal("Ventas", centro.GetProperty("departamentoNombre").GetString());

        var baja = await admin.DeleteAsync($"{Departamentos}/{departamentoId}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, baja.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Centros}/{centroId}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Departamentos}/{departamentoId}?versionFila=1")).StatusCode);

        var inexistente = await admin.PostAsJsonAsync(Centros, new { codigo = Ayudas.Codigo("CC"), nombre = "X", departamentoId = 999999 });
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
    }

    [Fact]
    public async Task Sin_permiso_o_sin_token_se_responde_403_y_401()
    {
        var (_, _, soloVer) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_BODEGAS_VER");

        Assert.Equal(HttpStatusCode.OK, (await soloVer.GetAsync(Bodegas)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await soloVer.PostAsJsonAsync(Bodegas, new { codigo = "X", nombre = "X", sucursalId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloVer.GetAsync(Cajas)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync(Bodegas)).StatusCode);
    }
}

/// <summary>Motor genérico de catálogos, probado con marcas y con formas/condiciones de pago.</summary>
[Collection(ColeccionApi.Nombre)]
public class CatalogosGeneralesTests(ApiFixture api)
{
    private const string Marcas = "/api/v1/catalogos/marcas";

    [Fact]
    public async Task Listado_paginado_con_busqueda_orden_y_filtro_de_estado()
    {
        var admin = await api.ClienteAdminAsync();
        var marca = $"Lote{Guid.NewGuid():N}"[..12];
        foreach (var letra in new[] { "C", "A", "B" })
            await admin.CrearAsync(Marcas, new { codigo = Ayudas.Codigo("M"), nombre = $"{marca} {letra}" });

        var pagina = await admin.GetFromJsonAsync<JsonElement>($"{Marcas}?q={marca}&sort=-nombre&page=1&pageSize=2");
        Assert.Equal(3, pagina.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, pagina.GetProperty("totalPages").GetInt32());
        Assert.Equal($"{marca} C", pagina.GetProperty("items")[0].GetProperty("nombre").GetString());

        var primero = pagina.GetProperty("items")[0];
        await admin.DeleteAsync($"{Marcas}/{primero.GetProperty("id").GetInt64()}?versionFila=1");
        var activas = await admin.GetFromJsonAsync<JsonElement>($"{Marcas}?q={marca}&activo=true");
        Assert.Equal(2, activas.GetProperty("totalItems").GetInt64());
        var inactivas = await admin.GetFromJsonAsync<JsonElement>($"{Marcas}?q={marca}&activo=false");
        Assert.Equal(1, inactivas.GetProperty("totalItems").GetInt64());

        // Campo de orden no permitido: se ignora; tamaño de página limitado.
        var tolerante = await admin.GetFromJsonAsync<JsonElement>($"{Marcas}?sort=password_hash&pageSize=5000");
        Assert.Equal(100, tolerante.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Los_datos_de_una_empresa_no_son_visibles_ni_modificables_desde_otra()
    {
        var admin = await api.ClienteAdminAsync();
        await api.EjecutarAsync(
            "INSERT IGNORE INTO empresa (id, codigo, razon_social, nombre_comercial, nit, moneda_local_id) VALUES (901, 'EMP901', 'Ajena', 'Ajena', 'CF', 1)");
        await api.EjecutarAsync("INSERT INTO marca (empresa_id, codigo, nombre) VALUES (901, 'AJENA', 'Marca de otra empresa') ON DUPLICATE KEY UPDATE nombre = nombre");
        var ajenaId = await api.EscalarAsync<long>("SELECT id FROM marca WHERE empresa_id = 901 AND codigo = 'AJENA'");

        var lista = await admin.GetFromJsonAsync<JsonElement>($"{Marcas}?q=Marca de otra empresa");
        Assert.Equal(0, lista.GetProperty("totalItems").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Marcas}/{ajenaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync($"{Marcas}/{ajenaId}", new { nombre = "Hackeada", activo = true, versionFila = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Marcas}/{ajenaId}?versionFila=1")).StatusCode);
        Assert.Equal("Marca de otra empresa", await api.EscalarAsync<string>("SELECT nombre FROM marca WHERE id = @i", ("@i", ajenaId)));

        // El mismo código en otra empresa no choca con el de la propia.
        var propia = await admin.PostAsJsonAsync(Marcas, new { codigo = "AJENA", nombre = "Propia" });
        Assert.Equal(HttpStatusCode.Created, propia.StatusCode);
    }

    [Fact]
    public async Task Ocho_altas_simultaneas_del_mismo_codigo_dejan_una_sola_y_el_resto_responde_409()
    {
        var codigo = Ayudas.Codigo("M");
        var clientes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => api.ClienteAdminAsync()));

        var respuestas = await Task.WhenAll(clientes.Select(c => c.PostAsJsonAsync(Marcas, new { codigo, nombre = "Simultánea" })));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await api.EscalarAsync<int>("SELECT COUNT(*) FROM marca WHERE codigo = @c", ("@c", codigo)));
        // La única que se creó dejó su auditoría; las rechazadas no dejaron nada.
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria a JOIN marca m ON CAST(m.id AS CHAR) = a.entidad_id WHERE a.entidad = 'marca' AND a.accion = 'CREAR' AND m.codigo = @c",
            ("@c", codigo)));
    }

    [Fact]
    public async Task Los_datos_invalidos_responden_400_con_errores_por_campo()
    {
        var admin = await api.ClienteAdminAsync();
        var respuesta = await admin.PostAsJsonAsync(Marcas, new { codigo = "con espacios", nombre = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.LeerAsync()).GetProperty("errors");
        Assert.True(errores.TryGetProperty("codigo", out _));
        Assert.True(errores.TryGetProperty("nombre", out _));
    }

    [Fact]
    public async Task Una_marca_inexistente_responde_404_y_desactivar_dos_veces_es_inocuo()
    {
        var admin = await api.ClienteAdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Marcas}/999999")).StatusCode);

        var id = (await admin.CrearAsync(Marcas, new { codigo = Ayudas.Codigo("M"), nombre = "Doble baja" })).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Marcas}/{id}?versionFila=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Marcas}/{id}?versionFila=2")).StatusCode);
    }

    [Fact]
    public async Task La_exportacion_devuelve_csv_neutraliza_formulas_y_queda_auditada()
    {
        var admin = await api.ClienteAdminAsync();
        var marca = $"Exp{Guid.NewGuid():N}"[..12];
        await admin.CrearAsync(Marcas, new { codigo = Ayudas.Codigo("M"), nombre = $"=SUMA(1) {marca}" });
        var antes = await api.EscalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE entidad = 'marca' AND accion = 'EXPORTAR'");

        var respuesta = await admin.GetAsync($"{Marcas}/exportar?q={marca}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Código,Nombre,Activo", csv);
        Assert.Contains($"'=SUMA(1) {marca}", csv);
        Assert.Equal(antes + 1, await api.EscalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE entidad = 'marca' AND accion = 'EXPORTAR'"));
    }

    [Fact]
    public async Task Forma_de_pago_y_condicion_de_pago_validan_sus_datos_propios()
    {
        var admin = await api.ClienteAdminAsync();

        var forma = await admin.CrearAsync("/api/v1/catalogos/formas-pago", new { codigo = Ayudas.Codigo("FP"), nombre = "Cheque", tipo = "BANCARIO", requiereReferencia = true });
        Assert.True(forma.GetProperty("requiereReferencia").GetBoolean());
        Assert.Equal("BANCARIO", forma.GetProperty("tipo").GetString());

        var tipoMalo = await admin.PostAsJsonAsync("/api/v1/catalogos/formas-pago", new { codigo = Ayudas.Codigo("FP"), nombre = "X", tipo = "TRUEQUE" });
        Assert.Equal(HttpStatusCode.BadRequest, tipoMalo.StatusCode);

        var condicion = await admin.CrearAsync("/api/v1/catalogos/condiciones-pago", new { codigo = Ayudas.Codigo("CP"), nombre = "A 45 días", diasCredito = 45 });
        Assert.Equal(45, condicion.GetProperty("diasCredito").GetInt32());

        var diasMalos = await admin.PostAsJsonAsync("/api/v1/catalogos/condiciones-pago", new { codigo = Ayudas.Codigo("CP"), nombre = "X", diasCredito = 4000 });
        Assert.Equal(HttpStatusCode.BadRequest, diasMalos.StatusCode);
    }

    [Fact]
    public async Task Cuenta_bancaria_exige_banco_y_moneda_y_no_repite_el_numero()
    {
        var admin = await api.ClienteAdminAsync();
        var bancoId = (await admin.CrearAsync("/api/v1/catalogos/bancos", new { codigo = Ayudas.Codigo("BAN"), nombre = "Banco de pruebas" })).GetProperty("id").GetInt64();
        var numero = Ayudas.Codigo("", 10);
        object Cuenta(string numeroCuenta) => new
        {
            codigo = Ayudas.Codigo("CTA"), nombre = "Cuenta operativa", bancoId, monedaId = 1, numeroCuenta, tipoCuenta = "MONETARIA", titular = "Empresa",
        };

        var creada = await admin.CrearAsync("/api/v1/catalogos/cuentas-bancarias", Cuenta(numero));
        Assert.Equal("Banco de pruebas", creada.GetProperty("bancoNombre").GetString());
        Assert.Equal("GTQ", creada.GetProperty("monedaCodigo").GetString());

        var repetida = await admin.PostAsJsonAsync("/api/v1/catalogos/cuentas-bancarias", Cuenta(numero));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        var bancoMalo = await admin.PostAsJsonAsync("/api/v1/catalogos/cuentas-bancarias",
            new { codigo = Ayudas.Codigo("CTA"), nombre = "X", bancoId = 999999, monedaId = 1, numeroCuenta = Ayudas.Codigo("", 10), tipoCuenta = "AHORRO" });
        Assert.Equal(HttpStatusCode.BadRequest, bancoMalo.StatusCode);

        // El banco no se desactiva mientras tenga cuentas activas.
        var baja = await admin.DeleteAsync($"/api/v1/catalogos/bancos/{bancoId}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, baja.StatusCode);
    }
}

/// <summary>Monedas y tipo de cambio.</summary>
[Collection(ColeccionApi.Nombre)]
public class MonedasYTiposCambioTests(ApiFixture api)
{
    private const string Monedas = "/api/v1/catalogos/monedas";
    private const string TiposCambio = "/api/v1/catalogos/tipos-cambio";

    private async Task<long> IdMonedaAsync(string codigo) =>
        await api.EscalarAsync<long>("SELECT id FROM moneda WHERE codigo = @c", ("@c", codigo));

    [Fact]
    public async Task La_moneda_local_no_se_puede_desactivar_y_una_nueva_exige_codigo_ISO()
    {
        var admin = await api.ClienteAdminAsync();
        var gtq = await IdMonedaAsync("GTQ");

        var baja = await admin.DeleteAsync($"{Monedas}/{gtq}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, baja.StatusCode);
        Assert.Equal("MONEDA_LOCAL", await baja.ErrorCodeAsync());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(Monedas, new { codigo = "EURO", nombre = "Euro", simbolo = "€" })).StatusCode);

        // Una moneda global: la crea una empresa y la ven todas.
        var codigo = new string(Guid.NewGuid().ToByteArray().Take(3).Select(b => (char)('A' + b % 26)).ToArray());
        if (await api.EscalarAsync<int>("SELECT COUNT(*) FROM moneda WHERE codigo = @c", ("@c", codigo)) == 0)
        {
            var creada = await admin.CrearAsync(Monedas, new { codigo, nombre = "Moneda de prueba", simbolo = "¤", decimales = 2 });
            Assert.Equal(JsonValueKind.Null, creada.GetProperty("empresaId").ValueKind);
        }
    }

    [Fact]
    public async Task Tipo_de_cambio_crear_duplicado_vigente_y_correccion()
    {
        var admin = await api.ClienteAdminAsync();
        var usd = await IdMonedaAsync("USD");
        var gtq = await IdMonedaAsync("GTQ");
        var fecha = new DateOnly(2020, 3, 10); // fecha propia de esta prueba

        var creada = await admin.CrearAsync(TiposCambio, new { monedaId = usd, fecha = fecha.ToString("yyyy-MM-dd"), tasa = 7.65432101m });
        var id = creada.GetProperty("id").GetInt64();
        Assert.Equal(7.65432101m, creada.GetProperty("tasa").GetDecimal());
        Assert.Equal("USD", creada.GetProperty("monedaCodigo").GetString());

        // Misma moneda y fecha: duplicado.
        var repetida = await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = fecha.ToString("yyyy-MM-dd"), tasa = 7.7m });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        // La moneda local no lleva tipo de cambio.
        var local = await admin.PostAsJsonAsync(TiposCambio, new { monedaId = gtq, fecha = "2020-03-11", tasa = 1m });
        Assert.Equal((HttpStatusCode)422, local.StatusCode);
        Assert.Equal("MONEDA_LOCAL", await local.ErrorCodeAsync());

        // Tasa inválida y fecha demasiado futura.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = "2020-03-12", tasa = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(TiposCambio, new { monedaId = usd, fecha = "2099-01-01", tasa = 7.7m })).StatusCode);

        // Para una fecha posterior sin tasa propia rige la última anterior.
        var vigente = await admin.GetFromJsonAsync<JsonElement>($"{TiposCambio}/vigente?monedaId={usd}&fecha=2020-03-20");
        Assert.Equal(7.65432101m, vigente.GetProperty("tasa").GetDecimal());
        Assert.Equal("2020-03-10", vigente.GetProperty("fechaTasa").GetString());

        // Antes de la primera tasa no hay ninguna: 404. La moneda local vale 1.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{TiposCambio}/vigente?monedaId={usd}&fecha=2000-01-01")).StatusCode);
        var localVigente = await admin.GetFromJsonAsync<JsonElement>($"{TiposCambio}/vigente?monedaId={gtq}&fecha=2020-03-20");
        Assert.Equal(1m, localVigente.GetProperty("tasa").GetDecimal());

        // Corrección con control de versión; la bitácora conserva el valor anterior.
        var corregida = await admin.PutAsJsonAsync($"{TiposCambio}/{id}", new { tasa = 7.7m, versionFila = 1 });
        Assert.Equal(HttpStatusCode.OK, corregida.StatusCode);
        var vieja = await admin.PutAsJsonAsync($"{TiposCambio}/{id}", new { tasa = 7.8m, versionFila = 1 });
        Assert.Equal(HttpStatusCode.Conflict, vieja.StatusCode);
        Assert.Equal("7.65432101", await api.EscalarAsync<string>(
            "SELECT JSON_UNQUOTE(JSON_EXTRACT(valores_anteriores, '$.tasa')) FROM auditoria WHERE entidad = 'tipo_cambio' AND entidad_id = @i AND accion = 'EDITAR'",
            ("@i", id.ToString())));
    }
}

/// <summary>Impuestos con vigencia.</summary>
[Collection(ColeccionApi.Nombre)]
public class ImpuestosTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/catalogos/impuestos";

    [Fact]
    public async Task Una_vigencia_nueva_no_puede_traslaparse_con_la_abierta_y_se_encadena_al_cerrarla()
    {
        var admin = await api.ClienteAdminAsync();
        var codigo = Ayudas.Codigo("IMP", 10);

        var primera = await admin.CrearAsync(Ruta, new { codigo, nombre = "Impuesto de prueba", tipo = "OTRO", porcentaje = 5m, vigenteDesde = "2030-01-01" });
        var primeraId = primera.GetProperty("id").GetInt64();

        var traslapada = await admin.PostAsJsonAsync(Ruta, new { codigo, nombre = "Nuevo", tipo = "OTRO", porcentaje = 6m, vigenteDesde = "2031-01-01" });
        Assert.Equal((HttpStatusCode)422, traslapada.StatusCode);
        Assert.Equal("VIGENCIA_TRASLAPADA", await traslapada.ErrorCodeAsync());

        var mismoInicio = await admin.PostAsJsonAsync(Ruta, new { codigo, nombre = "Nuevo", tipo = "OTRO", porcentaje = 6m, vigenteDesde = "2030-01-01" });
        Assert.Equal(HttpStatusCode.Conflict, mismoInicio.StatusCode);

        // Se cierra la primera y entonces la segunda cabe.
        var cierre = await admin.PutAsJsonAsync($"{Ruta}/{primeraId}", new { nombre = "Impuesto de prueba", vigenteHasta = "2030-12-31", activo = true, versionFila = 1 });
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        var segunda = await admin.CrearAsync(Ruta, new { codigo, nombre = "Impuesto de prueba", tipo = "OTRO", porcentaje = 6m, vigenteDesde = "2031-01-01" });
        Assert.Equal(6m, segunda.GetProperty("porcentaje").GetDecimal());

        // Reabrir la primera la traslaparía con la segunda.
        var reabrir = await admin.PutAsJsonAsync($"{Ruta}/{primeraId}", new { nombre = "Impuesto de prueba", vigenteHasta = (string?)null, activo = true, versionFila = 2 });
        Assert.Equal((HttpStatusCode)422, reabrir.StatusCode);

        // Filtro por vigencia: a 2030-06-01 rige solo la primera.
        var vigentes = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}?q={codigo}&vigentesA=2030-06-01");
        Assert.Equal(1, vigentes.GetProperty("totalItems").GetInt64());
        Assert.Equal(5m, vigentes.GetProperty("items")[0].GetProperty("porcentaje").GetDecimal());
    }

    [Fact]
    public async Task El_porcentaje_fuera_de_rango_y_el_IVA_sembrado()
    {
        var admin = await api.ClienteAdminAsync();
        var malo = await admin.PostAsJsonAsync(Ruta, new { codigo = Ayudas.Codigo("IMP", 10), nombre = "X", tipo = "IVA", porcentaje = 120m, vigenteDesde = "2030-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);

        var iva = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}?q=IVA&vigentesA=2026-10-10");
        Assert.Contains(iva.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("codigo").GetString() == "IVA" && i.GetProperty("porcentaje").GetDecimal() == 12m);
    }
}

/// <summary>Unidades de medida y sus conversiones.</summary>
[Collection(ColeccionApi.Nombre)]
public class UnidadesTests(ApiFixture api)
{
    private const string Unidades = "/api/v1/catalogos/unidades-medida";
    private const string Conversiones = "/api/v1/catalogos/conversiones-unidad";

    private async Task<long> IdEstandarAsync(string codigo) =>
        await api.EscalarAsync<long>("SELECT id FROM unidad_medida WHERE codigo = @c AND empresa_id IS NULL", ("@c", codigo));

    [Fact]
    public async Task Las_unidades_estandar_son_de_solo_lectura_y_su_codigo_esta_reservado()
    {
        var admin = await api.ClienteAdminAsync();
        var kg = await IdEstandarAsync("KG");

        var lista = await admin.GetFromJsonAsync<JsonElement>($"{Unidades}?q=Kilogramo");
        Assert.Contains(lista.GetProperty("items").EnumerateArray(), u => u.GetProperty("id").GetInt64() == kg && u.GetProperty("empresaId").ValueKind == JsonValueKind.Null);

        var editar = await admin.PutAsJsonAsync($"{Unidades}/{kg}", new { nombre = "Kilo", decimales = 3, activo = true, versionFila = 1 });
        Assert.Equal((HttpStatusCode)422, editar.StatusCode);
        Assert.Equal("ESTANDAR_SOLO_LECTURA", await editar.ErrorCodeAsync());

        var baja = await admin.DeleteAsync($"{Unidades}/{kg}?versionFila=1");
        Assert.Equal((HttpStatusCode)422, baja.StatusCode);

        var repetida = await admin.PostAsJsonAsync(Unidades, new { codigo = "kg", nombre = "Mi kilo", magnitud = "PESO", decimales = 3 });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task Conversion_entre_unidades_propias_rechaza_magnitud_distinta_ciclos_y_estandar()
    {
        var admin = await api.ClienteAdminAsync();
        var caja = (await admin.CrearAsync(Unidades, new { codigo = Ayudas.Codigo("CJ", 8), nombre = "Caja de 12", magnitud = "CANTIDAD", decimales = 0 })).GetProperty("id").GetInt64();
        var bulto = (await admin.CrearAsync(Unidades, new { codigo = Ayudas.Codigo("BL", 8), nombre = "Bulto", magnitud = "CANTIDAD", decimales = 0 })).GetProperty("id").GetInt64();
        var uni = await IdEstandarAsync("UNI");
        var kg = await IdEstandarAsync("KG");

        // 1 caja = 12 unidades
        var conversion = await admin.CrearAsync(Conversiones, new { unidadOrigenId = caja, unidadDestinoId = uni, factor = 12m });
        Assert.Equal("CANTIDAD", conversion.GetProperty("magnitud").GetString());
        var conversionId = conversion.GetProperty("id").GetInt64();

        // Magnitudes distintas
        var distinta = await admin.PostAsJsonAsync(Conversiones, new { unidadOrigenId = bulto, unidadDestinoId = kg, factor = 5m });
        Assert.Equal((HttpStatusCode)422, distinta.StatusCode);
        Assert.Equal("MAGNITUD_DISTINTA", await distinta.ErrorCodeAsync());

        // Ya hay camino caja → unidad: la inversa y un segundo camino son redundantes.
        var inversa = await admin.PostAsJsonAsync(Conversiones, new { unidadOrigenId = uni, unidadDestinoId = caja, factor = 0.083333m });
        Assert.Equal((HttpStatusCode)422, inversa.StatusCode);
        Assert.Equal("CONVERSION_REDUNDANTE", await inversa.ErrorCodeAsync());

        // bulto → caja sí es nuevo; pero bulto → unidad cerraría el triángulo.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Conversiones, new { unidadOrigenId = bulto, unidadDestinoId = caja, factor = 4m })).StatusCode);
        var triangulo = await admin.PostAsJsonAsync(Conversiones, new { unidadOrigenId = bulto, unidadDestinoId = uni, factor = 48m });
        Assert.Equal((HttpStatusCode)422, triangulo.StatusCode);

        // Factor con demasiados decimales: validación.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(Conversiones, new { unidadOrigenId = caja, unidadDestinoId = bulto, factor = 1.1234567m })).StatusCode);

        // Editar el factor y desactivar la conversión propia.
        var editada = await admin.PutAsJsonAsync($"{Conversiones}/{conversionId}", new { factor = 24m, activo = true, versionFila = 1 });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        Assert.Equal(24m, (await editada.LeerAsync()).GetProperty("factor").GetDecimal());

        // Una conversión estándar no se toca.
        var estandar = await api.EscalarAsync<long>("SELECT id FROM unidad_conversion WHERE empresa_id IS NULL LIMIT 1");
        var sobreEstandar = await admin.PutAsJsonAsync($"{Conversiones}/{estandar}", new { factor = 2m, activo = true, versionFila = 1 });
        Assert.Equal((HttpStatusCode)422, sobreEstandar.StatusCode);
    }

    [Fact]
    public async Task Unidad_con_mas_de_seis_decimales_o_magnitud_invalida_responde_400()
    {
        var admin = await api.ClienteAdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(Unidades, new { codigo = Ayudas.Codigo("U", 8), nombre = "X", magnitud = "PESO", decimales = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(Unidades, new { codigo = Ayudas.Codigo("U", 8), nombre = "X", magnitud = "MAGIA", decimales = 0 })).StatusCode);
    }
}

/// <summary>Series y correlativos: la pieza que no debe saltarse ni repetir números.</summary>
[Collection(ColeccionApi.Nombre)]
public class NumeracionTests(ApiFixture api)
{
    private const string Series = "/api/v1/catalogos/series-documento";

    private async Task<long> TipoDocumentoAsync(string codigo) =>
        await api.EscalarAsync<long>("SELECT id FROM tipo_documento WHERE codigo = @c", ("@c", codigo));

    private async Task<long> CrearSerieAsync(
        HttpClient admin, string serie, long siguiente = 1, long? maximo = null, string desde = "2026-01-01", string? hasta = null,
        string tipo = "FACTURA_VENTA", bool incluirAnio = false, string? prefijo = null)
    {
        var creada = await admin.CrearAsync(Series, new
        {
            tipoDocumentoId = await TipoDocumentoAsync(tipo), serie, prefijo, incluirAnio, longitud = 6,
            siguienteCorrelativo = siguiente, numeroMaximo = maximo, vigenteDesde = desde, vigenteHasta = hasta,
        });
        return creada.GetProperty("id").GetInt64();
    }

    private async Task<long> SiguienteGuardadoAsync(long serieId) =>
        await api.EscalarAsync<long>("SELECT siguiente_correlativo FROM serie_documento WHERE id = @i", ("@i", serieId));

    [Fact]
    public async Task Cincuenta_asignaciones_concurrentes_no_saltan_ni_repiten_numeros()
    {
        var admin = await api.ClienteAdminAsync();
        var serieId = await CrearSerieAsync(admin, Ayudas.Codigo("C", 8), siguiente: 100);

        var tareas = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            await using var alcance = api.Servicios.CreateAsyncScope();
            var numeracion = alcance.ServiceProvider.GetRequiredService<INumeracion>();
            return (await numeracion.SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None)).Numero;
        })).ToArray();

        var numeros = (await Task.WhenAll(tareas)).Order().ToArray();

        Assert.Equal(Enumerable.Range(100, 50).Select(n => (long)n), numeros);
        Assert.Equal(150, await SiguienteGuardadoAsync(serieId));
    }

    [Fact]
    public async Task Si_el_documento_se_revierte_el_numero_vuelve_a_la_serie()
    {
        var admin = await api.ClienteAdminAsync();
        var serieId = await CrearSerieAsync(admin, Ayudas.Codigo("R", 8));

        await using (var alcance = api.Servicios.CreateAsyncScope())
        {
            var unidad = alcance.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var numeracion = alcance.ServiceProvider.GetRequiredService<INumeracion>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => unidad.EjecutarEnTransaccionAsync(async () =>
            {
                var asignado = await numeracion.SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None);
                Assert.Equal(1, asignado.Numero);
                throw new InvalidOperationException("el documento falló después de numerarse");
            }));
        }

        Assert.Equal(1, await SiguienteGuardadoAsync(serieId));

        await using var otro = api.Servicios.CreateAsyncScope();
        var siguiente = await otro.ServiceProvider.GetRequiredService<INumeracion>()
            .SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None);
        Assert.Equal(1, siguiente.Numero);
    }

    [Fact]
    public async Task Una_serie_agotada_inactiva_o_fuera_de_vigencia_se_rechaza_sin_consumir_numero()
    {
        var admin = await api.ClienteAdminAsync();
        var agotada = await CrearSerieAsync(admin, Ayudas.Codigo("G", 8), siguiente: 1, maximo: 2);
        var vencida = await CrearSerieAsync(admin, Ayudas.Codigo("V", 8), desde: "2026-01-01", hasta: "2026-06-30");
        var futura = await CrearSerieAsync(admin, Ayudas.Codigo("F", 8), desde: "2027-01-01");
        var inactiva = await CrearSerieAsync(admin, Ayudas.Codigo("I", 8));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Series}/{inactiva}?versionFila=1")).StatusCode);

        async Task<ErpException> Falla(long serieId)
        {
            await using var alcance = api.Servicios.CreateAsyncScope();
            return await Assert.ThrowsAnyAsync<ErpException>(() => alcance.ServiceProvider.GetRequiredService<INumeracion>()
                .SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None));
        }

        async Task<long> Asigna(long serieId)
        {
            await using var alcance = api.Servicios.CreateAsyncScope();
            return (await alcance.ServiceProvider.GetRequiredService<INumeracion>()
                .SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None)).Numero;
        }

        Assert.Equal(1, await Asigna(agotada));
        Assert.Equal(2, await Asigna(agotada));
        Assert.Equal("SERIE_AGOTADA", (await Falla(agotada)).ErrorCode);
        Assert.Equal(3, await SiguienteGuardadoAsync(agotada));

        Assert.Equal("SERIE_FUERA_DE_VIGENCIA", (await Falla(vencida)).ErrorCode);
        Assert.Equal("SERIE_FUERA_DE_VIGENCIA", (await Falla(futura)).ErrorCode);
        Assert.Equal("SERIE_INACTIVA", (await Falla(inactiva)).ErrorCode);
        Assert.Equal(1, await SiguienteGuardadoAsync(vencida));
        Assert.Equal(1, await SiguienteGuardadoAsync(inactiva));

        Assert.IsType<NoEncontradoException>(await Falla(999999));
    }

    [Fact]
    public async Task El_numero_se_formatea_con_prefijo_anio_serie_y_ceros()
    {
        var admin = await api.ClienteAdminAsync();
        var serie = Ayudas.Codigo("S", 6);
        var serieId = await CrearSerieAsync(admin, serie, siguiente: 7, prefijo: "fac", incluirAnio: true, tipo: "FACTURA_COMPRA");

        await using var alcance = api.Servicios.CreateAsyncScope();
        var asignado = await alcance.ServiceProvider.GetRequiredService<INumeracion>()
            .SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None);

        Assert.Equal($"FAC-2026-{serie}-000007", asignado.NumeroFormateado);
    }

    [Fact]
    public async Task Editar_una_serie_no_mueve_el_correlativo_y_el_maximo_no_baja_de_lo_emitido()
    {
        var admin = await api.ClienteAdminAsync();
        var serieId = await CrearSerieAsync(admin, Ayudas.Codigo("E", 8), siguiente: 50, maximo: 100);

        var bajoLoEmitido = await admin.PutAsJsonAsync($"{Series}/{serieId}", new { numeroMaximo = 10, vigenteHasta = (string?)null, activo = true, versionFila = 1 });
        Assert.Equal((HttpStatusCode)422, bajoLoEmitido.StatusCode);
        Assert.Equal("MAXIMO_YA_EMITIDO", await bajoLoEmitido.ErrorCodeAsync());

        var ampliada = await admin.PutAsJsonAsync($"{Series}/{serieId}", new { numeroMaximo = 500, vigenteHasta = "2026-12-31", activo = true, versionFila = 1 });
        Assert.Equal(HttpStatusCode.OK, ampliada.StatusCode);
        var dto = await ampliada.LeerAsync();
        Assert.Equal(500, dto.GetProperty("numeroMaximo").GetInt64());
        Assert.Equal(50, dto.GetProperty("siguienteCorrelativo").GetInt64());

        // Editar la serie mientras se emiten documentos no pisa el correlativo en curso.
        await using (var alcance = api.Servicios.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<INumeracion>().SiguienteAsync(1, serieId, new DateOnly(2026, 10, 10), CancellationToken.None);
        var otra = await admin.PutAsJsonAsync($"{Series}/{serieId}", new { numeroMaximo = 600, vigenteHasta = "2026-12-31", activo = true, versionFila = 2 });
        Assert.Equal(51, (await otra.LeerAsync()).GetProperty("siguienteCorrelativo").GetInt64());
    }

    [Fact]
    public async Task Validaciones_de_alta_de_serie_y_duplicado()
    {
        var admin = await api.ClienteAdminAsync();
        var serie = Ayudas.Codigo("D", 8);
        await CrearSerieAsync(admin, serie);

        var repetida = await admin.PostAsJsonAsync(Series, new
        {
            tipoDocumentoId = await TipoDocumentoAsync("FACTURA_VENTA"), serie, longitud = 6, siguienteCorrelativo = 1, vigenteDesde = "2026-01-01",
        });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        // Mismo código de serie en otro tipo de documento sí es válido.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Series, new
        {
            tipoDocumentoId = await TipoDocumentoAsync("NOTA_CREDITO_VENTA"), serie, longitud = 6, siguienteCorrelativo = 1, vigenteDesde = "2026-01-01",
        })).StatusCode);

        var tipoInexistente = await admin.PostAsJsonAsync(Series, new
        {
            tipoDocumentoId = 999999, serie = Ayudas.Codigo("X", 8), longitud = 6, siguienteCorrelativo = 1, vigenteDesde = "2026-01-01",
        });
        Assert.Equal(HttpStatusCode.BadRequest, tipoInexistente.StatusCode);

        var cajaSinSucursal = await admin.PostAsJsonAsync(Series, new
        {
            tipoDocumentoId = await TipoDocumentoAsync("FACTURA_VENTA"), serie = Ayudas.Codigo("X", 8), cajaId = 1, longitud = 6,
            siguienteCorrelativo = 1, vigenteDesde = "2026-01-01",
        });
        Assert.Equal(HttpStatusCode.BadRequest, cajaSinSucursal.StatusCode);
    }

    [Fact]
    public async Task Los_tipos_de_documento_se_renombran_pero_no_se_crean()
    {
        var admin = await api.ClienteAdminAsync();
        var id = await TipoDocumentoAsync("COTIZACION");
        var original = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/catalogos/tipos-documento/{id}");

        var editado = await admin.PutAsJsonAsync($"/api/v1/catalogos/tipos-documento/{id}",
            new { nombre = "Cotización al cliente", activo = true, versionFila = original.GetProperty("versionFila").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        Assert.Equal("COTIZACION", (await editado.LeerAsync()).GetProperty("codigo").GetString());

        var crear = await admin.PostAsJsonAsync("/api/v1/catalogos/tipos-documento", new { codigo = "NUEVO", nombre = "Nuevo" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, crear.StatusCode);
    }
}

/// <summary>Configuración de negocio y fecha de bloqueo.</summary>
[Collection(ColeccionApi.Nombre)]
public class ConfiguracionTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/catalogos/configuracion";

    [Fact]
    public async Task Lista_los_parametros_con_su_valor_por_defecto_y_permite_cambiarlos_con_historial()
    {
        var admin = await api.ClienteAdminAsync();
        var lista = await admin.GetFromJsonAsync<JsonElement>(Ruta);
        var claves = lista.EnumerateArray().Select(c => c.GetProperty("clave").GetString()).ToArray();
        Assert.Contains("FECHA_BLOQUEO", claves);
        Assert.Contains("MONTO_MAXIMO_CONSUMIDOR_FINAL", claves);

        var monto = lista.EnumerateArray().Single(c => c.GetProperty("clave").GetString() == "MONTO_MAXIMO_CONSUMIDOR_FINAL");
        Assert.Equal("2500", monto.GetProperty("valor").GetString());

        var cambio = await admin.PutAsJsonAsync($"{Ruta}/STOCK_NEGATIVO", new { valor = "true" });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var dto = await cambio.LeerAsync();
        Assert.Equal("true", dto.GetProperty("valor").GetString());
        Assert.False(dto.GetProperty("esPredeterminado").GetBoolean());

        // Segundo cambio el mismo día: se reutiliza la fila (UNIQUE empresa, clave, vigente_desde).
        var otra = await admin.PutAsJsonAsync($"{Ruta}/STOCK_NEGATIVO", new { valor = "false" });
        Assert.Equal("false", (await otra.LeerAsync()).GetProperty("valor").GetString());
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM configuracion WHERE empresa_id = 1 AND clave = 'STOCK_NEGATIVO' AND activo = 1"));

        // Quitar el valor propio vuelve al predeterminado.
        var quitar = await admin.PutAsJsonAsync($"{Ruta}/STOCK_NEGATIVO", new { valor = (string?)null });
        Assert.True((await quitar.LeerAsync()).GetProperty("esPredeterminado").GetBoolean());

        Assert.True(await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria WHERE entidad = 'configuracion' AND entidad_id = 'STOCK_NEGATIVO'") >= 3);
    }

    [Fact]
    public async Task Valores_invalidos_y_claves_desconocidas_se_rechazan()
    {
        var admin = await api.ClienteAdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{Ruta}/STOCK_NEGATIVO", new { valor = "quizas" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{Ruta}/REDONDEO_IMPUESTO", new { valor = "SEMANAL" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{Ruta}/MONTO_MAXIMO_CONSUMIDOR_FINAL", new { valor = "-5" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/CLAVE_INVENTADA", new { valor = "x" })).StatusCode);

        var porRutaGenerica = await admin.PutAsJsonAsync($"{Ruta}/FECHA_BLOQUEO", new { valor = "2026-01-01" });
        Assert.Equal((HttpStatusCode)422, porRutaGenerica.StatusCode);
        Assert.Equal("USAR_FECHA_BLOQUEO", await porRutaGenerica.ErrorCodeAsync());
    }

    [Fact]
    public async Task La_fecha_de_bloqueo_impide_documentos_en_periodos_cerrados_y_tiene_su_propio_permiso()
    {
        var admin = await api.ClienteAdminAsync();
        var hoy = ERP.Domain.Catalogos.FechaNegocio.Hoy(DateTime.UtcNow);

        try
        {
            // Sin bloqueo cualquier fecha pasa.
            await using (var inicial = api.Servicios.CreateAsyncScope())
                await inicial.ServiceProvider.GetRequiredService<IPeriodoBloqueo>().ValidarFechaAsync(1, new DateOnly(2000, 1, 1), CancellationToken.None);

            var fijada = await admin.PutAsJsonAsync($"{Ruta}/fecha-bloqueo", new { fecha = hoy.AddDays(-10).ToString("yyyy-MM-dd") });
            Assert.Equal(HttpStatusCode.OK, fijada.StatusCode);

            await using (var alcance = api.Servicios.CreateAsyncScope())
            {
                var bloqueo = alcance.ServiceProvider.GetRequiredService<IPeriodoBloqueo>();
                // Igual o anterior: bloqueada. Posterior: libre.
                var exacta = await Assert.ThrowsAsync<ReglaNegocioException>(() => bloqueo.ValidarFechaAsync(1, hoy.AddDays(-10), CancellationToken.None));
                Assert.Equal("PERIODO_BLOQUEADO", exacta.ErrorCode);
                await Assert.ThrowsAsync<ReglaNegocioException>(() => bloqueo.ValidarFechaAsync(1, hoy.AddDays(-11), CancellationToken.None));
                await bloqueo.ValidarFechaAsync(1, hoy.AddDays(-9), CancellationToken.None);
                // Otra empresa no se ve afectada.
                await bloqueo.ValidarFechaAsync(901, hoy.AddDays(-30), CancellationToken.None);
            }

            // Una fecha futura cerraría documentos de hoy: no se admite.
            var futura = await admin.PutAsJsonAsync($"{Ruta}/fecha-bloqueo", new { fecha = hoy.AddDays(5).ToString("yyyy-MM-dd") });
            Assert.Equal(HttpStatusCode.BadRequest, futura.StatusCode);

            // El permiso de configuración no basta para cambiar el bloqueo.
            var (_, _, soloConfiguracion) = await api.CrearUsuarioConPermisosAsync("CATALOGOS_CONFIGURACION_VER", "CATALOGOS_CONFIGURACION_EDITAR");
            Assert.Equal(HttpStatusCode.OK, (await soloConfiguracion.GetAsync(Ruta)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await soloConfiguracion.PutAsJsonAsync($"{Ruta}/fecha-bloqueo", new { fecha = hoy.AddDays(-1).ToString("yyyy-MM-dd") })).StatusCode);
        }
        finally
        {
            var quitar = await admin.PutAsJsonAsync($"{Ruta}/fecha-bloqueo", new { fecha = (string?)null });
            Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        }

        await using var despues = api.Servicios.CreateAsyncScope();
        await despues.ServiceProvider.GetRequiredService<IPeriodoBloqueo>().ValidarFechaAsync(1, new DateOnly(2000, 1, 1), CancellationToken.None);
    }
}

/// <summary>Bodegas y cajas asignadas a un usuario.</summary>
[Collection(ColeccionApi.Nombre)]
public class AccesosOperativosTests(ApiFixture api)
{
    [Fact]
    public async Task Asignar_valida_sucursal_del_usuario_y_el_usuario_ve_solo_las_suyas()
    {
        var admin = await api.ClienteAdminAsync();
        var (nombre, usuarioId, cliente) = await api.CrearUsuarioConPermisosAsync("ORGANIZACION_BODEGAS_VER");
        _ = nombre;

        var propia = (await admin.CrearAsync("/api/v1/organizacion/bodegas", new { codigo = Ayudas.Codigo("BOD"), nombre = "Bodega asignada", sucursalId = 1 })).GetProperty("id").GetInt64();
        var otraSucursal = (await admin.CrearAsync("/api/v1/organizacion/sucursales", new { codigo = Ayudas.Codigo("SUC", 10), nombre = "Sucursal sin acceso" })).GetProperty("id").GetInt64();
        var ajena = (await admin.CrearAsync("/api/v1/organizacion/bodegas", new { codigo = Ayudas.Codigo("BOD"), nombre = "Bodega ajena", sucursalId = otraSucursal })).GetProperty("id").GetInt64();
        var caja = (await admin.CrearAsync("/api/v1/organizacion/cajas", new { codigo = Ayudas.Codigo("CAJ"), nombre = "Caja asignada", sucursalId = 1, bodegaId = propia })).GetProperty("id").GetInt64();

        var ruta = $"/api/v1/seguridad/usuarios/{usuarioId}/bodegas-cajas";

        // El usuario solo tiene acceso a la sucursal 1.
        var invalida = await admin.PutAsJsonAsync(ruta, new { bodegaIds = new[] { ajena }, cajaIds = Array.Empty<long>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.True((await invalida.LeerAsync()).GetProperty("errors").TryGetProperty("bodegaIds", out _));

        var guardada = await admin.PutAsJsonAsync(ruta, new { bodegaIds = new[] { propia }, cajaIds = new[] { caja } });
        Assert.Equal(HttpStatusCode.OK, guardada.StatusCode);
        Assert.Equal(propia, (await guardada.LeerAsync()).GetProperty("bodegaIds")[0].GetInt64());

        var mias = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/mis-bodegas-cajas");
        Assert.Equal(propia, mias.GetProperty("bodegas")[0].GetProperty("id").GetInt64());
        Assert.Equal(caja, mias.GetProperty("cajas")[0].GetProperty("id").GetInt64());

        // Quien no es administrador de usuarios no puede asignar.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync(ruta, new { bodegaIds = Array.Empty<long>(), cajaIds = Array.Empty<long>() })).StatusCode);

        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM auditoria WHERE entidad = 'usuario_acceso_operativo' AND entidad_id = @i", ("@i", usuarioId.ToString())));

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/seguridad/usuarios/999999/bodegas-cajas")).StatusCode);
    }
}

/// <summary>Seeds de la etapa 1A y datos de instalación.</summary>
[Collection(ColeccionApi.Nombre)]
public class SeedsCatalogosTests(ApiFixture api)
{
    [Fact]
    public async Task La_instalacion_trae_monedas_unidades_tipos_de_documento_y_catalogos_de_la_empresa()
    {
        var admin = await api.ClienteAdminAsync();

        Assert.Equal(2, await api.EscalarAsync<int>("SELECT COUNT(*) FROM moneda WHERE codigo IN ('GTQ', 'USD')"));
        Assert.Equal(1, await api.EscalarAsync<int>(
            "SELECT COUNT(*) FROM empresa e JOIN moneda m ON m.id = e.moneda_local_id WHERE e.id = 1 AND m.codigo = 'GTQ'"));
        Assert.True(await api.EscalarAsync<int>("SELECT COUNT(*) FROM tipo_documento") >= 10);
        Assert.True(await api.EscalarAsync<int>("SELECT COUNT(*) FROM unidad_conversion WHERE empresa_id IS NULL") >= 10);

        var formas = await admin.GetFromJsonAsync<JsonElement>("/api/v1/catalogos/formas-pago?pageSize=100");
        Assert.Contains(formas.GetProperty("items").EnumerateArray(), f => f.GetProperty("codigo").GetString() == "EFECTIVO");

        var tipos = await admin.GetFromJsonAsync<JsonElement>("/api/v1/catalogos/tipos-producto");
        var servicio = tipos.GetProperty("items").EnumerateArray().Single(t => t.GetProperty("codigo").GetString() == "SERVICIO");
        Assert.False(servicio.GetProperty("manejaInventario").GetBoolean());
    }

    [Fact]
    public async Task El_menu_del_administrador_incluye_catalogos_con_sus_grupos()
    {
        var admin = await api.ClienteAdminAsync();
        var menu = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/menu");

        var catalogos = menu.EnumerateArray().Single(m => m.GetProperty("codigo").GetString() == "MNU_CATALOGOS");
        var grupos = catalogos.GetProperty("hijos").EnumerateArray().Select(g => g.GetProperty("codigo").GetString()).ToArray();
        Assert.Contains("GRP_CAT_PRODUCTOS", grupos);
        Assert.Contains("GRP_CAT_FINANZAS", grupos);
        Assert.Contains("MNU_CAT_CONFIGURACION", grupos);

        var productos = catalogos.GetProperty("hijos").EnumerateArray().Single(g => g.GetProperty("codigo").GetString() == "GRP_CAT_PRODUCTOS");
        Assert.Contains(productos.GetProperty("hijos").EnumerateArray(), p => p.GetProperty("ruta").GetString() == "/catalogos/marcas");
    }

    [Fact]
    public async Task Un_usuario_solo_ve_en_el_menu_los_catalogos_para_los_que_tiene_permiso()
    {
        var (_, _, cliente) = await api.CrearUsuarioConPermisosAsync("CATALOGOS_MARCAS_VER");
        var menu = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/menu");

        var catalogos = menu.EnumerateArray().Single(m => m.GetProperty("codigo").GetString() == "MNU_CATALOGOS");
        var rutas = new List<string?>();
        void Recoger(JsonElement nodo)
        {
            foreach (var h in nodo.GetProperty("hijos").EnumerateArray())
            {
                if (h.GetProperty("tipo").GetString() == "PANTALLA")
                    rutas.Add(h.GetProperty("ruta").GetString());
                Recoger(h);
            }
        }

        Recoger(catalogos);
        Assert.Equal(["/catalogos/marcas"], rutas);
    }
}
