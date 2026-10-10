using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>Catálogo de referencia: estas pruebas fijan el comportamiento que deben repetir los demás catálogos.</summary>
[Collection(ColeccionApi.Nombre)]
public class SucursalesTests(ApiFixture api)
{
    private const string Ruta = "/api/v1/organizacion/sucursales";

    private static string Codigo() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private static async Task<JsonElement> CrearAsync(HttpClient cliente, string codigo, string nombre = "Sucursal de prueba")
    {
        var respuesta = await cliente.PostAsJsonAsync(Ruta, new { codigo, nombre, direccion = "Zona 1", telefono = "2222-0000", correo = "suc@pruebas.local" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Crear_editar_y_desactivar_deja_auditoria_de_cada_operacion()
    {
        var admin = await api.ClienteAdminAsync();
        var codigo = Codigo();

        var creada = await CrearAsync(admin, codigo.ToLowerInvariant());
        var id = creada.GetProperty("id").GetInt64();
        Assert.Equal(codigo, creada.GetProperty("codigo").GetString());
        Assert.Equal(1u, creada.GetProperty("versionFila").GetUInt32());

        var editada = await admin.PutAsJsonAsync($"{Ruta}/{id}", new
        {
            nombre = "Nombre editado", direccion = (string?)null, telefono = "2222-1111", correo = (string?)null, activo = true, versionFila = 1,
        });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        var trasEditar = await editada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Nombre editado", trasEditar.GetProperty("nombre").GetString());
        Assert.Equal(2u, trasEditar.GetProperty("versionFila").GetUInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/{id}?versionFila=2")).StatusCode);
        var trasBaja = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        Assert.False(trasBaja.GetProperty("activo").GetBoolean());

        foreach (var accion in new[] { "CREAR", "EDITAR", "ELIMINAR" })
            Assert.Equal(1, await api.EscalarAsync<int>(
                "SELECT COUNT(*) FROM auditoria WHERE entidad = 'sucursal' AND entidad_id = @id AND accion = @a AND usuario_id = 1 AND empresa_id = 1 AND operacion_id IS NOT NULL",
                ("@id", id.ToString()), ("@a", accion)));

        var nombreAnterior = await api.EscalarAsync<string>(
            "SELECT JSON_UNQUOTE(JSON_EXTRACT(valores_anteriores, '$.nombre')) FROM auditoria WHERE entidad = 'sucursal' AND entidad_id = @id AND accion = 'EDITAR'",
            ("@id", id.ToString()));
        Assert.Equal("Sucursal de prueba", nombreAnterior);
    }

    [Fact]
    public async Task El_correlation_id_de_la_solicitud_queda_en_la_auditoria()
    {
        var admin = await api.ClienteAdminAsync();
        var operacion = Guid.NewGuid().ToString("N");
        var solicitud = new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new { codigo = Codigo(), nombre = "Con correlation id" }),
        };
        solicitud.Headers.Add("X-Correlation-Id", operacion);

        var respuesta = await admin.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(operacion, respuesta.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal(1, await api.EscalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE operacion_id = @o", ("@o", operacion)));
    }

    [Fact]
    public async Task Un_codigo_repetido_responde_409()
    {
        var admin = await api.ClienteAdminAsync();
        var codigo = Codigo();
        await CrearAsync(admin, codigo);

        var repetida = await admin.PostAsJsonAsync(Ruta, new { codigo, nombre = "Otra" });

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("DUPLICADO", await repetida.ErrorCodeAsync());
    }

    [Fact]
    public async Task Editar_con_una_version_vieja_responde_409()
    {
        var admin = await api.ClienteAdminAsync();
        var id = (await CrearAsync(admin, Codigo())).GetProperty("id").GetInt64();
        var cuerpo = new { nombre = "Primera edición", direccion = (string?)null, telefono = (string?)null, correo = (string?)null, activo = true, versionFila = 1 };

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{Ruta}/{id}", cuerpo)).StatusCode);
        var segunda = await admin.PutAsJsonAsync($"{Ruta}/{id}", cuerpo with { nombre = "Edición concurrente" });

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("CONCURRENCIA", await segunda.ErrorCodeAsync());
        Assert.Equal("Primera edición", (await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}")).GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Los_datos_invalidos_responden_400_con_errores_por_campo()
    {
        var admin = await api.ClienteAdminAsync();

        var respuesta = await admin.PostAsJsonAsync(Ruta, new { codigo = "con espacios", nombre = "", correo = "no-es-correo" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDACION", problema.GetProperty("errorCode").GetString());
        var errores = problema.GetProperty("errors");
        Assert.True(errores.TryGetProperty("codigo", out _));
        Assert.True(errores.TryGetProperty("nombre", out _));
        Assert.True(errores.TryGetProperty("correo", out _));
    }

    [Fact]
    public async Task La_sucursal_principal_no_se_puede_desactivar()
    {
        var admin = await api.ClienteAdminAsync();
        var principal = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/1");

        var respuesta = await admin.DeleteAsync($"{Ruta}/1?versionFila={principal.GetProperty("versionFila").GetUInt32()}");

        Assert.Equal((HttpStatusCode)422, respuesta.StatusCode);
        Assert.Equal("SUCURSAL_PRINCIPAL", await respuesta.ErrorCodeAsync());
    }

    [Fact]
    public async Task Una_sucursal_inexistente_responde_404()
    {
        var admin = await api.ClienteAdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Ruta}/999999")).StatusCode);
    }

    [Fact]
    public async Task El_listado_pagina_busca_y_ordena_en_el_servidor()
    {
        var admin = await api.ClienteAdminAsync();
        var marca = $"Lote{Guid.NewGuid():N}"[..12];
        foreach (var letra in new[] { "C", "A", "B" })
            await CrearAsync(admin, Codigo(), $"{marca} {letra}");

        var pagina = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}?q={marca}&sort=-nombre&page=1&pageSize=2");

        Assert.Equal(3, pagina.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, pagina.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, pagina.GetProperty("items").GetArrayLength());
        Assert.Equal($"{marca} C", pagina.GetProperty("items")[0].GetProperty("nombre").GetString());

        // Un campo de orden fuera de la lista blanca se ignora; el tamaño de página se limita a 100.
        var tolerante = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}?q={marca}&sort=password_hash&pageSize=5000");
        Assert.Equal(100, tolerante.GetProperty("pageSize").GetInt32());
        Assert.Equal($"{marca} A", tolerante.GetProperty("items")[0].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task La_exportacion_devuelve_csv_y_queda_auditada()
    {
        var admin = await api.ClienteAdminAsync();
        var marca = $"Exp{Guid.NewGuid():N}"[..12];
        await CrearAsync(admin, Codigo(), $"=1+1 {marca}");
        var antes = await api.EscalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE entidad = 'sucursal' AND accion = 'EXPORTAR'");

        var respuesta = await admin.GetAsync($"{Ruta}/exportar?q={marca}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/csv", respuesta.Content.Headers.ContentType?.MediaType);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("Código,Nombre", csv.TrimStart('﻿'));
        // El valor que parecía fórmula sale neutralizado con una comilla simple.
        Assert.Contains($"'=1+1 {marca}", csv);
        Assert.Equal(antes + 1, await api.EscalarAsync<int>("SELECT COUNT(*) FROM auditoria WHERE entidad = 'sucursal' AND accion = 'EXPORTAR'"));
    }

    [Fact]
    public async Task La_bitacora_muestra_las_operaciones_con_el_nombre_del_usuario()
    {
        var admin = await api.ClienteAdminAsync();
        var id = (await CrearAsync(admin, Codigo())).GetProperty("id").GetInt64();

        var bitacora = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/auditoria/bitacora?entidad=sucursal&q={id}&pageSize=100");

        var fila = bitacora.GetProperty("items").EnumerateArray().First(i => i.GetProperty("entidadId").GetString() == id.ToString());
        Assert.Equal("admin", fila.GetProperty("nombreUsuario").GetString());
        Assert.Equal("CREAR", fila.GetProperty("accion").GetString());
        Assert.Contains("Sucursal de prueba", fila.GetProperty("valoresNuevos").GetString());
    }
}
