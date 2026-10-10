using System.Reflection;
using System.Threading.RateLimiting;
using ERP.Api.Controllers.Seguridad;
using ERP.Api.Middleware;
using ERP.Api.Seguridad;
using ERP.Application.Auditoria;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Application.Seguridad;
using ERP.Infrastructure;
using ERP.Infrastructure.Persistencia;
using ERP.Infrastructure.Seguridad;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var configuracion = builder.Configuration;

// ----- Logging: consola + archivo rotativo, con el correlation id en cada línea -----
builder.Host.UseSerilog((contexto, logger) => logger
    .ReadFrom.Configuration(contexto.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {OperacionId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(contexto.Configuration["Logs:Carpeta"] ?? "logs", "erp-api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: contexto.Configuration.GetValue("Logs:DiasRetencion", 30),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {OperacionId} {SourceContext} {Message:lj}{NewLine}{Exception}"));

// ----- Infraestructura y casos de uso -----
builder.Services.AddInfraestructura(configuracion);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
builder.Services.AddSingleton(new OpcionesSeguridad { UrlFrontend = configuracion["Frontend:Url"] ?? "http://localhost:3000" });

builder.Services.AddScoped<AutorizacionService>();
builder.Services.AddScoped<IAccesosUsuarioActual>(sp => sp.GetRequiredService<AutorizacionService>());
builder.Services.AddScoped<AutenticacionService>();
builder.Services.AddScoped<UsuariosService>();
builder.Services.AddScoped<RolesService>();
builder.Services.AddScoped<AccesosOperativosService>();
builder.Services.AddScoped<ModulosService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<EmpresasService>();
builder.Services.AddScoped<SucursalesService>();
builder.Services.AddScoped<BitacoraService>();

// Etapa 1A: catálogos (motor genérico + los que tienen lógica propia) y numeración.
builder.Services.AddScoped(typeof(CatalogoService<,,>));
builder.Services.AddScoped<TiposCambioService>();
builder.Services.AddScoped<ImpuestosService>();
builder.Services.AddScoped<ConversionesUnidadService>();
builder.Services.AddScoped<SeriesDocumentoService>();
builder.Services.AddScoped<NumeracionService>();
builder.Services.AddScoped<INumeracion>(sp => sp.GetRequiredService<NumeracionService>());
builder.Services.AddScoped<ConfiguracionService>();
builder.Services.AddScoped<IConfiguracionNegocio>(sp => sp.GetRequiredService<ConfiguracionService>());
builder.Services.AddScoped<IPeriodoBloqueo>(sp => sp.GetRequiredService<ConfiguracionService>());

ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("es");
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

// ----- API -----
builder.Services.AddControllers(opciones => opciones.Filters.Add<FiltroValidacion>());
builder.Services.Configure<ApiBehaviorOptions>(opciones =>
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        var errores = contexto.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => FiltroValidacion.CamelCase(e.Key.TrimStart('$', '.')),
                e => e.Value!.Errors.Select(_ => "El valor no es válido.").Distinct().ToArray());
        var problema = Problemas.Crear(contexto.HttpContext, 400, "VALIDACION", "Datos inválidos",
            "La solicitud contiene datos inválidos.", errores);
        return new ObjectResult(problema) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
    });
builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// ----- Autenticación: JWT de acceso corto. Los permisos no viajan en el token -----
var jwt = configuracion.GetSection("Jwt").Get<OpcionesJwt>() ?? new OpcionesJwt();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = jwt.ClaveDeFirma(),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
        };
        opciones.Events = new JwtBearerEvents
        {
            OnChallenge = async contexto =>
            {
                contexto.HandleResponse();
                await Problemas.EscribirAsync(contexto.HttpContext,
                    Problemas.Crear(contexto.HttpContext, 401, "NO_AUTENTICADO", "No autenticado", "Debe iniciar sesión."));
            },
        };
    });

// Si a un endpoint le faltara [Authorize], igual exige autenticación.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ----- Límite de solicitudes por IP en los endpoints de autenticación -----
var limitePorMinuto = configuracion.GetValue("LimiteTasa:AutenticacionPorMinuto", 10);
builder.Services.AddRateLimiter(opciones =>
{
    opciones.AddPolicy(AutenticacionController.LimiteAutenticacion, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limitePorMinuto, Window = TimeSpan.FromMinutes(1) }));
    opciones.OnRejected = async (contexto, ct) =>
        await Problemas.EscribirAsync(contexto.HttpContext,
            Problemas.Crear(contexto.HttpContext, 429, "DEMASIADAS_SOLICITUDES", "Demasiadas solicitudes",
                "Se superó el límite de intentos. Espere un minuto e intente de nuevo."), ct);
});

// El BFF de Next.js llama de servidor a servidor; CORS queda restringido a los orígenes configurados.
var origenes = configuracion.GetSection("Cors:Origenes").Get<string[]>() ?? [];
builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

// La IP real del cliente llega en X-Forwarded-For desde el BFF o el proxy inverso (solo se confía en loopback).
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<CabecerasSeguridadMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    // En desarrollo el BFF habla con la API por HTTP en localhost; fuera de él, solo HTTPS.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ----- Salud y versión -----
app.MapGet("/health", async (UnitOfWork unidadDeTrabajo, CancellationToken ct) =>
{
    try
    {
        var conexion = await unidadDeTrabajo.ConexionAsync(ct);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT 1";
        await comando.ExecuteScalarAsync(ct);
        return Results.Ok(new { estado = "ok", baseDeDatos = "ok" });
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        app.Logger.LogError(ex, "La comprobación de salud no pudo conectar con la base de datos");
        return Results.Json(new { estado = "error", baseDeDatos = "sin conexión" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapGet("/health/version", () => Results.Ok(new
{
    version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "desconocida",
    ambiente = app.Environment.EnvironmentName,
})).AllowAnonymous();

// La documentación de la API solo existe en Desarrollo y QA.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("QA"))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.Run();

/// <summary>Punto de entrada; se declara parcial para que las pruebas de integración puedan arrancar la API.</summary>
public partial class Program;
