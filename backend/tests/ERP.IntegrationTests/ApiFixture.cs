using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace ERP.IntegrationTests;

/// <summary>
/// Crea una base temporal <c>erp_test_*</c> ejecutando <c>database/database.sql</c> (así cada corrida valida
/// también el script maestro), arranca la API contra ella y la elimina al terminar.
/// Nunca toca otra base: el nombre se genera aquí y siempre empieza por <c>erp_test_</c>.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string PasswordInicial = "Cambiar.2026!";
    public const string PasswordAdmin = "Admin.Pruebas.2026!";

    private readonly string _baseDeDatos = $"erp_test_{Guid.NewGuid():N}"[..25];
    private string _cadenaServidor = "";
    private WebApplicationFactory<Program>? _fabrica;

    public string CadenaConexion { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<ApiFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        _cadenaServidor = configuracion.GetConnectionString("Pruebas")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Pruebas (servidor MySQL local y usuario con permiso para crear bases erp_test_*). " +
                "Configúrela con: dotnet user-secrets set \"ConnectionStrings:Pruebas\" \"Server=localhost;Port=3306;User ID=...;Password=...\" " +
                "--project tests/ERP.IntegrationTests");
        var claveJwt = configuracion["Jwt:Clave"] ?? "clave-solo-para-pruebas-de-integracion-0123456789";

        await using (var conexion = new MySqlConnection(_cadenaServidor))
        {
            await conexion.OpenAsync();

            var version = Version.Parse(conexion.ServerVersion.Split('-')[0]);
            if (version < new Version(8, 4))
                throw new InvalidOperationException($"Las pruebas requieren MySQL 8.4 o superior; el servidor es {conexion.ServerVersion}.");

            var script = (await File.ReadAllTextAsync(RutaScriptMaestro()))
                .Replace("CREATE DATABASE erpgt", $"CREATE DATABASE {_baseDeDatos}")
                .Replace("USE erpgt;", $"USE {_baseDeDatos};");
            if (!script.Contains($"CREATE DATABASE {_baseDeDatos}") || !script.Contains($"USE {_baseDeDatos};"))
                throw new InvalidOperationException("database.sql ya no contiene 'CREATE DATABASE erpgt' / 'USE erpgt;'; ajuste la sustitución del nombre.");

            await using var comando = new MySqlCommand(script, conexion) { CommandTimeout = 120 };
            await comando.ExecuteNonQueryAsync();
        }

        CadenaConexion = new MySqlConnectionStringBuilder(_cadenaServidor) { Database = _baseDeDatos }.ConnectionString;

        // Program.cs lee la configuración al arrancar; las variables de entorno son la forma fiable de sustituirla.
        Environment.SetEnvironmentVariable("ConnectionStrings__Erp", CadenaConexion);
        Environment.SetEnvironmentVariable("Jwt__Clave", claveJwt);
        Environment.SetEnvironmentVariable("LimiteTasa__AutenticacionPorMinuto", "100000");
        Environment.SetEnvironmentVariable("Correo__Modo", "Archivo");
        Environment.SetEnvironmentVariable("Correo__Carpeta", CarpetaCorreo);
        Environment.SetEnvironmentVariable("Logs__Carpeta", Path.Combine(Path.GetTempPath(), "erp-pruebas-logs"));

        _fabrica = new WebApplicationFactory<Program>();

        // El administrador sembrado debe cambiar su contraseña antes de poder administrar.
        var inicial = await IniciarSesionAsync("admin", PasswordInicial);
        var cambio = await inicial.PostAsJsonAsync("/api/v1/seguridad/autenticacion/cambiar-password",
            new { passwordActual = PasswordInicial, passwordNueva = PasswordAdmin });
        cambio.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (_fabrica is not null)
            await _fabrica.DisposeAsync();

        MySqlConnection.ClearAllPools();
        await using var conexion = new MySqlConnection(_cadenaServidor);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand($"DROP DATABASE IF EXISTS {_baseDeDatos}", conexion);
        await comando.ExecuteNonQueryAsync();

        if (Directory.Exists(CarpetaCorreo))
            Directory.Delete(CarpetaCorreo, recursive: true);
    }

    public string CarpetaCorreo { get; } = Path.Combine(Path.GetTempPath(), $"erp-pruebas-correo-{Guid.NewGuid():N}");

    public HttpClient ClienteAnonimo() => _fabrica!.CreateClient();

    public Task<HttpClient> ClienteAdminAsync() => IniciarSesionAsync("admin", PasswordAdmin);

    public async Task<HttpClient> IniciarSesionAsync(string usuario, string password)
    {
        var tokens = await LoginAsync(usuario, password);
        var cliente = _fabrica!.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return cliente;
    }

    public async Task<JsonElement> LoginAsync(string usuario, string password)
    {
        var respuesta = await ClienteAnonimo().PostAsJsonAsync("/api/v1/seguridad/autenticacion/login",
            new { nombreUsuario = usuario, password });
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Crea un rol con los permisos indicados y un usuario con ese rol, ya con su contraseña definitiva.
    /// Devuelve el nombre de usuario y un cliente autenticado.
    /// </summary>
    public async Task<(string Usuario, long UsuarioId, HttpClient Cliente)> CrearUsuarioConPermisosAsync(params string[] permisos)
    {
        var admin = await ClienteAdminAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var catalogo = await admin.GetFromJsonAsync<JsonElement>("/api/v1/seguridad/permisos");
        var ids = catalogo.EnumerateArray()
            .Where(p => permisos.Contains(p.GetProperty("codigo").GetString()))
            .Select(p => p.GetProperty("id").GetInt64())
            .ToArray();
        Assert.Equal(permisos.Length, ids.Length);

        var rol = await admin.PostAsJsonAsync("/api/v1/seguridad/roles",
            new { codigo = $"ROL_{sufijo}", nombre = $"Rol {sufijo}", descripcion = (string?)null, permisoIds = ids });
        rol.EnsureSuccessStatusCode();
        var rolId = (await rol.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var nombre = $"u{sufijo}";
        const string temporal = "Temporal.2026!x";
        const string definitiva = "Definitiva.2026!x";
        var usuario = await admin.PostAsJsonAsync("/api/v1/seguridad/usuarios", new
        {
            nombreUsuario = nombre,
            correo = $"{nombre}@pruebas.local",
            nombreCompleto = $"Usuario {sufijo}",
            passwordTemporal = temporal,
            rolIds = new[] { rolId },
            empresaIds = new[] { 1L },
            sucursalIds = new[] { 1L },
        });
        usuario.EnsureSuccessStatusCode();
        var usuarioId = (await usuario.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var primera = await IniciarSesionAsync(nombre, temporal);
        var cambio = await primera.PostAsJsonAsync("/api/v1/seguridad/autenticacion/cambiar-password",
            new { passwordActual = temporal, passwordNueva = definitiva });
        cambio.EnsureSuccessStatusCode();

        return (nombre, usuarioId, await IniciarSesionAsync(nombre, definitiva));
    }

    public async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var conexion = new MySqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
            comando.Parameters.AddWithValue(nombre, valor);
        return (T)Convert.ChangeType((await comando.ExecuteScalarAsync())!, typeof(T));
    }

    public async Task EjecutarAsync(string sql)
    {
        await using var conexion = new MySqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private static string RutaScriptMaestro()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var ruta = Path.Combine(carpeta.FullName, "database", "database.sql");
            if (File.Exists(ruta))
                return ruta;
        }

        throw new FileNotFoundException("No se encontró database/database.sql subiendo desde la carpeta de pruebas.");
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "Api";
}

public static class RespuestaExtensions
{
    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();
}
