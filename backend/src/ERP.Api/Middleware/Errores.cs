using ERP.Domain.Comun;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Api.Middleware;

/// <summary>Construye respuestas RFC 7807 con <c>errorCode</c> estable y <c>traceId</c>.</summary>
public static class Problemas
{
    public static ProblemDetails Crear(HttpContext http, int estado, string errorCode, string titulo, string? detalle = null,
        IReadOnlyDictionary<string, string[]>? errores = null)
    {
        var problema = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = detalle,
            Type = $"https://httpstatuses.io/{estado}",
            Instance = http.Request.Path,
        };
        problema.Extensions["errorCode"] = errorCode;
        problema.Extensions["traceId"] = http.TraceIdentifier;
        if (errores is not null)
            problema.Extensions["errors"] = errores;
        return problema;
    }

    public static Task EscribirAsync(HttpContext http, ProblemDetails problema, CancellationToken ct = default)
    {
        http.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return http.Response.WriteAsJsonAsync(problema, options: null, contentType: "application/problem+json", cancellationToken: ct);
    }
}

/// <summary>Traduce las excepciones a ProblemDetails. Un error inesperado responde 500 sin detalles internos.</summary>
public sealed class ManejadorExcepciones(ILogger<ManejadorExcepciones> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problema = exception switch
        {
            ValidacionException e => Problemas.Crear(http, 400, e.ErrorCode, "Datos inválidos", e.Message, e.Errores),
            NoAutenticadoException e => Problemas.Crear(http, 401, e.ErrorCode, "No autenticado", e.Message),
            AccesoDenegadoException e => Problemas.Crear(http, 403, e.ErrorCode, "Acceso denegado", e.Message),
            NoEncontradoException e => Problemas.Crear(http, 404, e.ErrorCode, "No encontrado", e.Message),
            ConflictoException e => Problemas.Crear(http, 409, e.ErrorCode, "Conflicto", e.Message),
            ReglaNegocioException e => Problemas.Crear(http, 422, e.ErrorCode, "Regla de negocio", e.Message),
            BadHttpRequestException => Problemas.Crear(http, 400, "SOLICITUD_INVALIDA", "Solicitud inválida", "La solicitud no se pudo interpretar."),
            _ => null,
        };

        if (problema is null)
        {
            if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
                return true;

            logger.LogError(exception, "Error no controlado en {Metodo} {Ruta}", http.Request.Method, http.Request.Path);
            problema = Problemas.Crear(http, 500, "ERROR_INTERNO", "Error interno",
                "Ocurrió un error inesperado. Si persiste, comunique el identificador de la operación al administrador.");
        }

        await Problemas.EscribirAsync(http, problema, ct);
        return true;
    }
}

/// <summary>
/// Ejecuta el validador de FluentValidation registrado para cada argumento de la acción.
/// Los nombres de campo se devuelven en camelCase, como los ve el frontend.
/// </summary>
public sealed class FiltroValidacion(IServiceProvider servicios) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null)
                continue;

            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());
            if (servicios.GetService(tipoValidador) is not IValidator validador)
                continue;

            var resultado = await validador.ValidateAsync(new ValidationContext<object>(argumento), context.HttpContext.RequestAborted);
            if (!resultado.IsValid)
                throw new ValidacionException(resultado.Errors
                    .GroupBy(e => CamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
        }

        await next();
    }

    internal static string CamelCase(string ruta) =>
        string.Join('.', ruta.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}
