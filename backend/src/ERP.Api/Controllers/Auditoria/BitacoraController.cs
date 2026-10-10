using ERP.Api.Seguridad;
using ERP.Application.Auditoria;
using ERP.Application.Comun;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Auditoria;

[ApiController]
[Authorize]
[Route("api/v1/auditoria/bitacora")]
public sealed class BitacoraController(BitacoraService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("AUDITORIA_BITACORA_VER")]
    public Task<ResultadoPaginado<BitacoraDto>> Listar([FromQuery] ConsultaBitacora consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);
}
