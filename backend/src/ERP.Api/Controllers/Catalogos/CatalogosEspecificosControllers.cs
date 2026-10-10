using ERP.Api.Seguridad;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Catalogos;

[ApiController]
[Authorize]
[Route("api/v1/catalogos/tipos-cambio")]
public sealed class TiposCambioController(TiposCambioService servicio, ITiposCambioConsulta consulta, IUsuarioActual actual) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_VER")]
    public Task<ResultadoPaginado<TipoCambioDto>> Listar([FromQuery] ConsultaTiposCambio consultaListado, CancellationToken ct) =>
        servicio.ListarAsync(consultaListado, ct);

    /// <summary>Tasa aplicable a una fecha: la del día o la última anterior (1 para la moneda local).</summary>
    [HttpGet("vigente")]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_VER")]
    public async Task<TasaVigente> Vigente([FromQuery] long monedaId, [FromQuery] DateOnly fecha, CancellationToken ct) =>
        await consulta.ObtenerVigenteAsync(actual.EmpresaIdRequerida(), monedaId, fecha, ct)
        ?? throw new NoEncontradoException("No hay un tipo de cambio registrado para esa moneda a esa fecha.");

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_VER")]
    public Task<TipoCambioDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_CREAR")]
    public async Task<IActionResult> Crear(CrearTipoCambioDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_EDITAR")]
    public Task<TipoCambioDto> Editar(long id, EditarTipoCambioDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_TIPOSCAMBIO_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaTiposCambio consultaListado, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consultaListado, ct);
        return CsvArchivo.Descarga(this, "tipos-cambio.csv", ["Fecha", "Moneda", "Tasa"],
            filas.Select(f => new string?[] { f.Fecha.ToString("yyyy-MM-dd"), f.MonedaCodigo, CsvArchivo.Numero(f.Tasa) }));
    }
}

[ApiController]
[Authorize]
[Route("api/v1/catalogos/impuestos")]
public sealed class ImpuestosController(ImpuestosService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_IMPUESTOS_VER")]
    public Task<ResultadoPaginado<ImpuestoDto>> Listar([FromQuery] ConsultaImpuestos consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_IMPUESTOS_VER")]
    public Task<ImpuestoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_IMPUESTOS_CREAR")]
    public async Task<IActionResult> Crear(CrearImpuestoDto dto, CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_IMPUESTOS_EDITAR")]
    public Task<ImpuestoDto> Editar(long id, EditarImpuestoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);

    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_IMPUESTOS_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }

    [HttpGet("exportar")]
    [RequierePermiso("CATALOGOS_IMPUESTOS_EXPORTAR")]
    public async Task<IActionResult> Exportar([FromQuery] ConsultaImpuestos consulta, CancellationToken ct)
    {
        var filas = await servicio.ExportarAsync(consulta, ct);
        return CsvArchivo.Descarga(this, "impuestos.csv", ["Código", "Nombre", "Tipo", "Porcentaje", "Desde", "Hasta", "Activo"],
            filas.Select(f => new string?[]
            {
                f.Codigo, f.Nombre, f.Tipo, CsvArchivo.Numero(f.Porcentaje), f.VigenteDesde.ToString("yyyy-MM-dd"),
                f.VigenteHasta?.ToString("yyyy-MM-dd"), CsvArchivo.SiNo(f.Activo),
            }));
    }
}

[ApiController]
[Authorize]
[Route("api/v1/catalogos/conversiones-unidad")]
public sealed class ConversionesUnidadController(ConversionesUnidadService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_CONVERSIONES_VER")]
    public Task<ResultadoPaginado<ConversionUnidadDto>> Listar([FromQuery] ConsultaConversiones consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_CONVERSIONES_VER")]
    public Task<ConversionUnidadDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_CONVERSIONES_CREAR")]
    public async Task<IActionResult> Crear(CrearConversionUnidadDto dto, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_CONVERSIONES_EDITAR")]
    public Task<ConversionUnidadDto> Editar(long id, EditarConversionUnidadDto dto, CancellationToken ct) =>
        servicio.EditarAsync(id, dto, ct);

    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_CONVERSIONES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/catalogos/series-documento")]
public sealed class SeriesDocumentoController(SeriesDocumentoService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_SERIES_VER")]
    public Task<ResultadoPaginado<SerieDocumentoDto>> Listar([FromQuery] ConsultaSeries consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_SERIES_VER")]
    public Task<SerieDocumentoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [RequierePermiso("CATALOGOS_SERIES_CREAR")]
    public async Task<IActionResult> Crear(CrearSerieDocumentoDto dto, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_SERIES_EDITAR")]
    public Task<SerieDocumentoDto> Editar(long id, EditarSerieDocumentoDto dto, CancellationToken ct) =>
        servicio.EditarAsync(id, dto, ct);

    [HttpDelete("{id:long}")]
    [RequierePermiso("CATALOGOS_SERIES_ELIMINAR")]
    public async Task<IActionResult> Desactivar(long id, [FromQuery] uint versionFila, CancellationToken ct)
    {
        await servicio.DesactivarAsync(id, versionFila, ct);
        return NoContent();
    }
}

/// <summary>Catálogo cerrado: se listan y se renombran/activan; no se crean ni se exportan desde la aplicación.</summary>
[ApiController]
[Authorize]
[Route("api/v1/catalogos/tipos-documento")]
public sealed class TiposDocumentoController(CatalogoService<TipoDocumentoDto, CrearTipoDocumentoDto, EditarTipoDocumentoDto> servicio)
    : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_TIPOSDOCUMENTO_VER")]
    public Task<ResultadoPaginado<TipoDocumentoDto>> Listar([FromQuery] ConsultaCatalogo consulta, CancellationToken ct) =>
        servicio.ListarAsync(consulta, ct);

    [HttpGet("{id:long}")]
    [RequierePermiso("CATALOGOS_TIPOSDOCUMENTO_VER")]
    public Task<TipoDocumentoDto> Obtener(long id, CancellationToken ct) => servicio.ObtenerAsync(id, ct);

    [HttpPut("{id:long}")]
    [RequierePermiso("CATALOGOS_GLOBALES_EDITAR")]
    public Task<TipoDocumentoDto> Editar(long id, EditarTipoDocumentoDto dto, CancellationToken ct) => servicio.EditarAsync(id, dto, ct);
}

[ApiController]
[Authorize]
[Route("api/v1/catalogos/configuracion")]
public sealed class ConfiguracionController(ConfiguracionService servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("CATALOGOS_CONFIGURACION_VER")]
    public Task<IReadOnlyList<ConfiguracionDto>> Listar(CancellationToken ct) => servicio.ListarAsync(ct);

    [HttpPut("{clave}")]
    [RequierePermiso("CATALOGOS_CONFIGURACION_EDITAR")]
    public Task<ConfiguracionDto> Establecer(string clave, EstablecerConfiguracionDto dto, CancellationToken ct) =>
        servicio.EstablecerAsync(clave, dto.Valor, ct);

    /// <summary>Fecha de bloqueo de documentos; con valor nulo se quita el bloqueo.</summary>
    [HttpPut("fecha-bloqueo")]
    [RequierePermiso("CATALOGOS_FECHABLOQUEO_EDITAR")]
    public Task<ConfiguracionDto> EstablecerFechaBloqueo(EstablecerFechaBloqueoDto dto, CancellationToken ct) =>
        servicio.EstablecerFechaBloqueoAsync(dto.Fecha, ct);
}

public sealed record EstablecerFechaBloqueoDto(DateOnly? Fecha);
