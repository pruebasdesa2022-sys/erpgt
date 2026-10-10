using ERP.Api.Seguridad;
using ERP.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.Api.Controllers.Seguridad;

[ApiController]
[Authorize]
[Route("api/v1/seguridad/autenticacion")]
public sealed class AutenticacionController(AutenticacionService servicio) : ControllerBase
{
    public const string LimiteAutenticacion = "autenticacion";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteAutenticacion)]
    public Task<TokensDto> Login(LoginDto dto, CancellationToken ct) => servicio.LoginAsync(dto, ct);

    /// <summary>Rota el refresh token. La credencial es el propio token, por eso no exige el de acceso.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteAutenticacion)]
    public Task<TokensDto> Refresh(RefreshDto dto, CancellationToken ct) => servicio.RefrescarAsync(dto, ct);

    /// <summary>Cambia la empresa y sucursal activas; emite tokens nuevos con ese contexto.</summary>
    [HttpPost("contexto")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteAutenticacion)]
    public Task<TokensDto> CambiarContexto(CambiarContextoDto dto, CancellationToken ct) => servicio.CambiarContextoAsync(dto, ct);

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshDto dto, CancellationToken ct)
    {
        await servicio.CerrarSesionAsync(dto, ct);
        return NoContent();
    }

    /// <summary>Responde siempre 202, exista o no el usuario.</summary>
    [HttpPost("recuperacion")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteAutenticacion)]
    public async Task<IActionResult> SolicitarRecuperacion(SolicitarRecuperacionDto dto, CancellationToken ct)
    {
        await servicio.SolicitarRecuperacionAsync(dto, ct);
        return Accepted();
    }

    [HttpPost("restablecer")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteAutenticacion)]
    public async Task<IActionResult> Restablecer(RestablecerPasswordDto dto, CancellationToken ct)
    {
        await servicio.RestablecerPasswordAsync(dto, ct);
        return NoContent();
    }

    /// <summary>Cambia la contraseña propia y cierra todas las sesiones del usuario.</summary>
    [HttpPost("cambiar-password")]
    [SoloAutenticado]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordDto dto, CancellationToken ct)
    {
        await servicio.CambiarPasswordAsync(dto, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/seguridad")]
public sealed class SesionController(AutenticacionService autenticacion, MenuService menu, AutorizacionService autorizacion) : ControllerBase
{
    [HttpGet("sesion")]
    [SoloAutenticado]
    public Task<SesionDto> Sesion(CancellationToken ct) => autenticacion.ObtenerSesionAsync(ct);

    /// <summary>Menú ya filtrado por estado efectivo del módulo y permisos del usuario.</summary>
    [HttpGet("menu")]
    [SoloAutenticado]
    public Task<IReadOnlyList<MenuItemDto>> Menu(CancellationToken ct) => menu.ObtenerAsync(ct);

    /// <summary>Códigos de permiso que el usuario puede ejercer ahora; el frontend los usa para mostrar u ocultar botones.</summary>
    [HttpGet("mis-permisos")]
    [SoloAutenticado]
    public Task<IReadOnlyList<string>> MisPermisos(CancellationToken ct) => autorizacion.PermisosEjerciblesAsync(ct);
}
