using ERP.Api.Seguridad;
using ERP.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Seguridad;

/// <summary>Bodegas y cajas a las que accede cada usuario.</summary>
[ApiController]
[Authorize]
[Route("api/v1/seguridad")]
public sealed class AccesosOperativosController(AccesosOperativosService servicio) : ControllerBase
{
    [HttpGet("usuarios/{id:long}/bodegas-cajas")]
    [RequierePermiso("SEGURIDAD_USUARIOS_VER")]
    public Task<AccesosOperativosDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPut("usuarios/{id:long}/bodegas-cajas")]
    [RequierePermiso("SEGURIDAD_USUARIOS_EDITAR")]
    public Task<AccesosOperativosDto> Guardar(long id, GuardarAccesosOperativosDto dto, CancellationToken ct) =>
        servicio.GuardarAsync(id, dto, ct);

    /// <summary>Bodegas y cajas del usuario autenticado en su empresa activa.</summary>
    [HttpGet("mis-bodegas-cajas")]
    [SoloAutenticado]
    public Task<MisAccesosOperativosDto> MisAccesos(CancellationToken ct) => servicio.MisAccesosAsync(ct);
}
