using System.Security.Claims;
using ERP.Application.Comun;
using ERP.Application.Seguridad;
using ERP.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ERP.Api.Seguridad;

/// <summary>
/// Exige un permiso para ejecutar la acción. Aplica, en este orden, los pasos de la cadena de
/// seguridad posteriores a la autenticación: contexto empresa/sucursal → módulo habilitado → permiso.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequierePermisoAttribute(string codigo) : Attribute, IAsyncAuthorizationFilter
{
    public string Codigo { get; } = codigo;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        context.HttpContext.RequestServices.GetRequiredService<AutorizacionService>()
            .VerificarAsync(Codigo, context.HttpContext.RequestAborted);
}

/// <summary>
/// Excepción explícita a <see cref="RequierePermisoAttribute"/> para acciones que todo usuario
/// autenticado puede usar sobre sí mismo (su sesión, su menú, cambiar su contraseña).
/// Sigue validando que el usuario esté activo y que su contexto empresa/sucursal sea válido.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SoloAutenticadoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        context.HttpContext.RequestServices.GetRequiredService<AutorizacionService>()
            .VerificarAsync(null, context.HttpContext.RequestAborted);
}

/// <summary>Datos de la solicitud HTTP en curso, leídos del token ya validado.</summary>
public sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    private HttpContext? Contexto => accesor.HttpContext;

    public long? UsuarioId => Leer(JwtRegisteredClaimNames.Sub);

    public long? EmpresaId => Leer(ClaimsErp.Empresa);

    public long? SucursalId => Leer(ClaimsErp.Sucursal);

    public string? Ip => Contexto?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Contexto?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agente ? agente : null;

    public string OperacionId => Contexto?.TraceIdentifier ?? "";

    private long? Leer(string claim)
    {
        var usuario = Contexto?.User;
        if (usuario?.Identity?.IsAuthenticated != true)
            return null;

        return long.TryParse(usuario.FindFirstValue(claim), out var valor) ? valor : null;
    }
}
