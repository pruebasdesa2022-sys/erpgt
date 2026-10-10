using System.Globalization;
using System.Text;
using ERP.Api.Seguridad;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Organizacion;

[ApiController]
[Authorize]
[Route("api/v1/organizacion/empresas")]
public sealed class EmpresasController(EmpresasService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_EMPRESAS_VER")]
    public Task<IReadOnlyList<EmpresaDto>> Listar(CancellationToken ct) => servicio.ListarAsync(ct);

    /// <summary>Edita los datos de la empresa. Las empresas no se crean ni se desactivan desde la aplicación.</summary>
    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_EMPRESAS_EDITAR")]
    public Task<EmpresaDto> Editar(long id, EditarEmpresaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);
}

/// <summary>Catálogo de referencia de Fase 0: el patrón CRUD que siguen los demás catálogos.</summary>
[ApiController]
[Authorize]
[Route("api/v1/organizacion/sucursales")]
public sealed class SucursalesController(SucursalesService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_SUCURSALES_VER")]
    public Task<ResultadoPaginado<SucursalDto>> Listar([FromQuery] ConsultaSucursales consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("ORGANIZACION_SUCURSALES_VER")]
    public Task<SucursalDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("ORGANIZACION_SUCURSALES_CREAR")]
    public async Task<IActionResult> Crear(CrearSucursalDto dto, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_SUCURSALES_EDITAR")]
    public Task<SucursalDto> Editar(long id, EditarSucursalDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("ORGANIZACION_SUCURSALES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    /// <summary>CSV en UTF-8 con BOM (para que Excel respete las tildes), con los mismos filtros del listado.</summary>
    [HttpGet("exportar")]
    [RequierePermiso("ORGANIZACION_SUCURSALES_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaSucursales consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);

        var csv = new StringBuilder("Código,Nombre,Dirección,Teléfono,Correo,Principal,Activo\r\n");
        foreach (var f in filas)
            csv.AppendJoin(',', Celda(f.Codigo), Celda(f.Nombre), Celda(f.Direccion), Celda(f.Telefono), Celda(f.Correo),
                f.EsPrincipal ? "Sí" : "No", f.Activo ? "Sí" : "No").Append("\r\n");

        var contenido = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(contenido, "text/csv; charset=utf-8", "sucursales.csv");
    }

    /// <summary>
    /// Escapa una celda de CSV. Los valores que empiezan con = + - @ se prefijan con comilla simple
    /// para que una hoja de cálculo no los ejecute como fórmula.
    /// </summary>
    private static string Celda(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return "";

        var texto = valor[0] is '=' or '+' or '-' or '@' ? "'" + valor : valor;
        return texto.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + texto.Replace("\"", "\"\"", true, CultureInfo.InvariantCulture) + "\""
            : texto;
    }
}
