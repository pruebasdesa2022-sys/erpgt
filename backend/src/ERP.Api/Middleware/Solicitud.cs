using System.Text.RegularExpressions;
using Serilog.Context;

namespace ERP.Api.Middleware;

/// <summary>
/// Asigna el correlation id de la solicitud: acepta el que envía el BFF si tiene forma segura
/// o genera uno. Queda en <c>TraceIdentifier</c>, en cada línea de log, en la auditoría y en la respuesta.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Cabecera = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext http)
    {
        var recibido = http.Request.Headers[Cabecera].ToString();
        var id = FormatoValido().IsMatch(recibido) ? recibido : Guid.NewGuid().ToString("N");

        http.TraceIdentifier = id;
        http.Response.Headers[Cabecera] = id;

        using (LogContext.PushProperty("OperacionId", id))
            await next(http);
    }

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex FormatoValido();
}

/// <summary>Cabeceras de seguridad para una API JSON: nada se guarda en caché ni se incrusta.</summary>
public sealed class CabecerasSeguridadMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        var cabeceras = http.Response.Headers;
        cabeceras.XContentTypeOptions = "nosniff";
        cabeceras.XFrameOptions = "DENY";
        cabeceras["Referrer-Policy"] = "no-referrer";
        // La página de documentación (solo Desarrollo/QA) carga sus propios scripts y estilos.
        if (!http.Request.Path.StartsWithSegments("/scalar"))
            cabeceras.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        cabeceras.CacheControl = "no-store";
        return next(http);
    }
}
