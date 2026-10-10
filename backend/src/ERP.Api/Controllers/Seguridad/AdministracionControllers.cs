using ERP.Api.Seguridad;
using ERP.Application.Comun;
using ERP.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Seguridad;

[ApiController]
[Authorize]
[Route("api/v1/seguridad/usuarios")]
public sealed class UsuariosController(UsuariosService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("SEGURIDAD_USUARIOS_VER")]
    public Task<ResultadoPaginado<UsuarioListaDto>> Listar([FromQuery] ConsultaUsuarios consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    /// <summary>Roles, empresas y sucursales disponibles para el formulario de usuario.</summary>
    [HttpGet("opciones")]
    [RequierePermiso("SEGURIDAD_USUARIOS_VER")]
    public Task<OpcionesUsuarioDto> Opciones(CancellationToken ct) => servicio.ObtenerOpcionesAsync(ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("SEGURIDAD_USUARIOS_VER")]
    public Task<UsuarioDetalleDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("SEGURIDAD_USUARIOS_CREAR")]
    public async Task<IActionResult> Crear(CrearUsuarioDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("SEGURIDAD_USUARIOS_EDITAR")]
    public Task<UsuarioDetalleDto> Editar(long id, EditarUsuarioDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("SEGURIDAD_USUARIOS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/restablecer-password")]
    [RequierePermiso("SEGURIDAD_USUARIOS_EDITAR")]
    public async Task<IActionResult> RestablecerPassword(long id, RestablecerPasswordAdminDto dto, CancellationToken ct)
    {
        await servicio.RestablecerPasswordAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/permisos")]
    [RequierePermiso("SEGURIDAD_PERMISOS_VER")]
    public Task<IReadOnlyList<PermisoDirectoDto>> PermisosDirectos(long id, CancellationToken ct) =>
        servicio.ObtenerPermisosDirectosAsync(id, ct);

    [HttpPut("{id:long}/permisos")]
    [RequierePermiso("SEGURIDAD_PERMISOS_EDITAR")]
    public Task<IReadOnlyList<PermisoDirectoDto>> GuardarPermisosDirectos(long id, GuardarPermisosDirectosDto dto, CancellationToken ct) =>
        servicio.GuardarPermisosDirectosAsync(id, dto, ct);
}

[ApiController]
[Authorize]
[Route("api/v1/seguridad/roles")]
public sealed class RolesController(RolesService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("SEGURIDAD_ROLES_VER")]
    public Task<ResultadoPaginado<RolListaDto>> Listar([FromQuery] ConsultaRoles consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("SEGURIDAD_ROLES_VER")]
    public Task<RolDetalleDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("SEGURIDAD_ROLES_CREAR")]
    public async Task<IActionResult> Crear(CrearRolDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("SEGURIDAD_ROLES_EDITAR")]
    public Task<RolDetalleDto> Editar(long id, EditarRolDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    [HttpDelete("{id:long}")]
    [RequierePermiso("SEGURIDAD_ROLES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/seguridad/permisos")]
public sealed class PermisosController(RolesService servicio) : ControllerBase
{
    /// <summary>Catálogo de permisos. Se administra por script, no desde la aplicación.</summary>
    [HttpGet]
    [RequierePermiso("SEGURIDAD_PERMISOS_VER")]
    public Task<IReadOnlyList<PermisoDto>> Listar(CancellationToken ct) => servicio.ListarPermisosAsync(ct);
}

[ApiController]
[Authorize]
[Route("api/v1/seguridad/modulos")]
public sealed class ModulosController(ModulosService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("SEGURIDAD_MODULOS_VER")]
    public Task<IReadOnlyList<ModuloDto>> Listar(CancellationToken ct) => servicio.ListarAsync(ct);

    /// <summary>Cambia el estado global o el de la empresa activa. Exige motivo y queda auditado.</summary>
    [HttpPut("{id:long}/estado")]
    [RequierePermiso("SEGURIDAD_MODULOS_EDITAR")]
    public Task<ModuloDto> CambiarEstado(long id, CambiarEstadoModuloDto dto, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(id, dto, ct);
}
