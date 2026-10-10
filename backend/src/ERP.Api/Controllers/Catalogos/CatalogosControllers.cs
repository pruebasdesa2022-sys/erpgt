using ERP.Api.Seguridad;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Catalogos;

/// <summary>bodegas: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/organizacion/bodegas")]
public sealed class BodegasController(CatalogoService<BodegaDto, CrearBodegaDto, EditarBodegaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_BODEGAS_VER")]
    public Task<ResultadoPaginado<BodegaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("ORGANIZACION_BODEGAS_VER")]
    public Task<BodegaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("ORGANIZACION_BODEGAS_CREAR")]
    public async Task<IActionResult> Crear(CrearBodegaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_BODEGAS_EDITAR")]
    public Task<BodegaDto> Editar(long id, EditarBodegaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("ORGANIZACION_BODEGAS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("ORGANIZACION_BODEGAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "bodegas.csv", ["Código", "Nombre", "Sucursal", "Descripción", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.SucursalNombre, f.Descripcion, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>cajas: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/organizacion/cajas")]
public sealed class CajasController(CatalogoService<CajaDto, CrearCajaDto, EditarCajaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_CAJAS_VER")]
    public Task<ResultadoPaginado<CajaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("ORGANIZACION_CAJAS_VER")]
    public Task<CajaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("ORGANIZACION_CAJAS_CREAR")]
    public async Task<IActionResult> Crear(CrearCajaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_CAJAS_EDITAR")]
    public Task<CajaDto> Editar(long id, EditarCajaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("ORGANIZACION_CAJAS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("ORGANIZACION_CAJAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "cajas.csv", ["Código", "Nombre", "Sucursal", "Bodega", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.SucursalNombre, f.BodegaNombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>departamentos: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/organizacion/departamentos")]
public sealed class DepartamentosController(CatalogoService<DepartamentoDto, CrearDepartamentoDto, EditarDepartamentoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_VER")]
    public Task<ResultadoPaginado<DepartamentoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_VER")]
    public Task<DepartamentoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_CREAR")]
    public async Task<IActionResult> Crear(CrearDepartamentoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_EDITAR")]
    public Task<DepartamentoDto> Editar(long id, EditarDepartamentoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("ORGANIZACION_DEPARTAMENTOS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "departamentos.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>centros-costo: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/organizacion/centros-costo")]
public sealed class CentrosCostoController(CatalogoService<CentroCostoDto, CrearCentroCostoDto, EditarCentroCostoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_VER")]
    public Task<ResultadoPaginado<CentroCostoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_VER")]
    public Task<CentroCostoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_CREAR")]
    public async Task<IActionResult> Crear(CrearCentroCostoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_EDITAR")]
    public Task<CentroCostoDto> Editar(long id, EditarCentroCostoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("ORGANIZACION_CENTROSCOSTO_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "centros-costo.csv", ["Código", "Nombre", "Departamento", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.DepartamentoNombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>tipos-producto: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/tipos-producto")]
public sealed class TiposProductoController(CatalogoService<TipoProductoDto, CrearTipoProductoDto, EditarTipoProductoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_VER")]
    public Task<ResultadoPaginado<TipoProductoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_VER")]
    public Task<TipoProductoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_CREAR")]
    public async Task<IActionResult> Crear(CrearTipoProductoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_EDITAR")]
    public Task<TipoProductoDto> Editar(long id, EditarTipoProductoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_TIPOSPRODUCTO_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "tipos-producto.csv", ["Código", "Nombre", "Maneja inventario", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.ManejaInventario), CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>categorias: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/categorias")]
public sealed class CategoriasController(CatalogoService<CategoriaDto, CrearCategoriaDto, EditarCategoriaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_CATEGORIAS_VER")]
    public Task<ResultadoPaginado<CategoriaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_CATEGORIAS_VER")]
    public Task<CategoriaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_CATEGORIAS_CREAR")]
    public async Task<IActionResult> Crear(CrearCategoriaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_CATEGORIAS_EDITAR")]
    public Task<CategoriaDto> Editar(long id, EditarCategoriaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_CATEGORIAS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_CATEGORIAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "categorias.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>marcas: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/marcas")]
public sealed class MarcasController(CatalogoService<MarcaDto, CrearMarcaDto, EditarMarcaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_MARCAS_VER")]
    public Task<ResultadoPaginado<MarcaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_MARCAS_VER")]
    public Task<MarcaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_MARCAS_CREAR")]
    public async Task<IActionResult> Crear(CrearMarcaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_MARCAS_EDITAR")]
    public Task<MarcaDto> Editar(long id, EditarMarcaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_MARCAS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_MARCAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "marcas.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>tipos-proveedor: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/tipos-proveedor")]
public sealed class TiposProveedorController(CatalogoService<TipoProveedorDto, CrearTipoProveedorDto, EditarTipoProveedorDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_VER")]
    public Task<ResultadoPaginado<TipoProveedorDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_VER")]
    public Task<TipoProveedorDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_CREAR")]
    public async Task<IActionResult> Crear(CrearTipoProveedorDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_EDITAR")]
    public Task<TipoProveedorDto> Editar(long id, EditarTipoProveedorDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_TIPOSPROVEEDOR_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "tipos-proveedor.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>formas-pago: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/formas-pago")]
public sealed class FormasPagoController(CatalogoService<FormaPagoDto, CrearFormaPagoDto, EditarFormaPagoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_FORMASPAGO_VER")]
    public Task<ResultadoPaginado<FormaPagoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_FORMASPAGO_VER")]
    public Task<FormaPagoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_FORMASPAGO_CREAR")]
    public async Task<IActionResult> Crear(CrearFormaPagoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_FORMASPAGO_EDITAR")]
    public Task<FormaPagoDto> Editar(long id, EditarFormaPagoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_FORMASPAGO_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_FORMASPAGO_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "formas-pago.csv", ["Código", "Nombre", "Tipo", "Requiere referencia", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.Tipo, CsvArchivo.SiNo(f.RequiereReferencia), CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>condiciones-pago: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/condiciones-pago")]
public sealed class CondicionesPagoController(CatalogoService<CondicionPagoDto, CrearCondicionPagoDto, EditarCondicionPagoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_VER")]
    public Task<ResultadoPaginado<CondicionPagoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_VER")]
    public Task<CondicionPagoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_CREAR")]
    public async Task<IActionResult> Crear(CrearCondicionPagoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_EDITAR")]
    public Task<CondicionPagoDto> Editar(long id, EditarCondicionPagoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_CONDICIONESPAGO_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "condiciones-pago.csv", ["Código", "Nombre", "Días de crédito", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.DiasCredito.ToString(), CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>motivos-anulacion: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/motivos-anulacion")]
public sealed class MotivosAnulacionController(CatalogoService<MotivoAnulacionDto, CrearMotivoAnulacionDto, EditarMotivoAnulacionDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_VER")]
    public Task<ResultadoPaginado<MotivoAnulacionDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_VER")]
    public Task<MotivoAnulacionDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_CREAR")]
    public async Task<IActionResult> Crear(CrearMotivoAnulacionDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_EDITAR")]
    public Task<MotivoAnulacionDto> Editar(long id, EditarMotivoAnulacionDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_MOTIVOSANULACION_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "motivos-anulacion.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>bancos: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/bancos")]
public sealed class BancosController(CatalogoService<BancoDto, CrearBancoDto, EditarBancoDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_BANCOS_VER")]
    public Task<ResultadoPaginado<BancoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_BANCOS_VER")]
    public Task<BancoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_BANCOS_CREAR")]
    public async Task<IActionResult> Crear(CrearBancoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_BANCOS_EDITAR")]
    public Task<BancoDto> Editar(long id, EditarBancoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_BANCOS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_BANCOS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "bancos.csv", ["Código", "Nombre", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>cuentas-bancarias: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/cuentas-bancarias")]
public sealed class CuentasBancariasController(CatalogoService<CuentaBancariaDto, CrearCuentaBancariaDto, EditarCuentaBancariaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_VER")]
    public Task<ResultadoPaginado<CuentaBancariaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_VER")]
    public Task<CuentaBancariaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_CREAR")]
    public async Task<IActionResult> Crear(CrearCuentaBancariaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_EDITAR")]
    public Task<CuentaBancariaDto> Editar(long id, EditarCuentaBancariaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_CUENTASBANCARIAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "cuentas-bancarias.csv", ["Código", "Nombre", "Banco", "Moneda", "Número de cuenta", "Tipo", "Titular", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.BancoNombre, f.MonedaCodigo, f.NumeroCuenta, f.TipoCuenta, f.Titular, CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>monedas: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/monedas")]
public sealed class MonedasController(CatalogoService<MonedaDto, CrearMonedaDto, EditarMonedaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_MONEDAS_VER")]
    public Task<ResultadoPaginado<MonedaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_MONEDAS_VER")]
    public Task<MonedaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_GLOBALES_EDITAR")]
    public async Task<IActionResult> Crear(CrearMonedaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_GLOBALES_EDITAR")]
    public Task<MonedaDto> Editar(long id, EditarMonedaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_GLOBALES_EDITAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_MONEDAS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "monedas.csv", ["Código", "Nombre", "Símbolo", "Decimales", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.Simbolo, f.Decimales.ToString(), CsvArchivo.SiNo(f.Activo) }));
    }
}

/// <summary>unidades-medida: catálogo sobre el motor genérico (ver CatalogoService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/unidades-medida")]
public sealed class UnidadesMedidaController(CatalogoService<UnidadMedidaDto, CrearUnidadMedidaDto, EditarUnidadMedidaDto> servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_UNIDADES_VER")]
    public Task<ResultadoPaginado<UnidadMedidaDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_UNIDADES_VER")]
    public Task<UnidadMedidaDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_UNIDADES_CREAR")]
    public async Task<IActionResult> Crear(CrearUnidadMedidaDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_UNIDADES_EDITAR")]
    public Task<UnidadMedidaDto> Editar(long id, EditarUnidadMedidaDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    /// <summary>Baja lógica. Requiere la versión de la fila para detectar ediciones concurrentes.</summary>
    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_UNIDADES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_UNIDADES_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "unidades-medida.csv", ["Código", "Nombre", "Magnitud", "Decimales", "Estándar", "Activo"],
            filas.Select(f => new string?[] { f.Codigo, f.Nombre, f.Magnitud, f.Decimales.ToString(), CsvArchivo.SiNo(f.EmpresaId is null), CsvArchivo.SiNo(f.Activo) }));
    }
}
