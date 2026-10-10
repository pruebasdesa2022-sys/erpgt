using ERP.Application.Catalogos;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Organizacion;

// Estructura de la empresa (etapa 1A): bodegas, cajas, departamentos y centros de costo.
// Usan el motor genérico de catálogos; aquí solo están sus datos propios y sus reglas.

// ----- Bodegas -----

public sealed record BodegaDto : CatalogoDto
{
    public long SucursalId { get; init; }
    public string SucursalNombre { get; init; } = "";
    public string? Descripcion { get; init; }
}

public sealed record CrearBodegaDto : CrearCatalogo
{
    public long SucursalId { get; init; }
    public string? Descripcion { get; init; }
}

/// <summary>La sucursal de una bodega no cambia: el inventario futuro depende de ella.</summary>
public sealed record EditarBodegaDto : EditarCatalogo
{
    public string? Descripcion { get; init; }
}

public sealed class CrearBodegaValidator : CrearCatalogoValidator<CrearBodegaDto>
{
    public CrearBodegaValidator()
    {
        RuleFor(x => x.SucursalId).GreaterThan(0).WithMessage("Seleccione la sucursal.");
        RuleFor(x => x.Descripcion).MaximumLength(300);
    }
}

public sealed class EditarBodegaValidator : EditarCatalogoValidator<EditarBodegaDto>
{
    public EditarBodegaValidator() => RuleFor(x => x.Descripcion).MaximumLength(300);
}

public sealed class BodegaReglas(IReferencias referencias) : IReglasCatalogo<BodegaDto, CrearBodegaDto, EditarBodegaDto>
{
    public async Task ValidarCrearAsync(long empresaId, CrearBodegaDto dto, CancellationToken ct)
    {
        if (!await referencias.ExisteActivaAsync(EntidadReferencia.Sucursal, dto.SucursalId, empresaId, ct))
            throw new ValidacionException("sucursalId", "La sucursal no existe o está inactiva.");
    }

    public async Task ValidarEditarAsync(long empresaId, BodegaDto anterior, EditarBodegaDto dto, CancellationToken ct)
    {
        if (!anterior.Activo && dto.Activo
            && !await referencias.ExisteActivaAsync(EntidadReferencia.Sucursal, anterior.SucursalId, empresaId, ct))
            throw new ReglaNegocioException("SUCURSAL_INACTIVA", "No se puede reactivar la bodega: su sucursal está inactiva.");
    }

    public async Task ValidarDesactivarAsync(long empresaId, BodegaDto anterior, CancellationToken ct)
    {
        if (await referencias.ContarDependientesActivosAsync(RelacionDependiente.CajasDeBodega, anterior.Id, ct) > 0)
            throw new ReglaNegocioException("TIENE_DEPENDIENTES", "La bodega tiene cajas activas asociadas. Desactívelas o cámbielas de bodega primero.");
    }
}

// ----- Cajas -----

public sealed record CajaDto : CatalogoDto
{
    public long SucursalId { get; init; }
    public string SucursalNombre { get; init; } = "";
    public long? BodegaId { get; init; }
    public string? BodegaNombre { get; init; }
}

public sealed record CrearCajaDto : CrearCatalogo
{
    public long SucursalId { get; init; }
    public long? BodegaId { get; init; }
}

public sealed record EditarCajaDto : EditarCatalogo
{
    public long? BodegaId { get; init; }
}

public sealed class CrearCajaValidator : CrearCatalogoValidator<CrearCajaDto>
{
    public CrearCajaValidator() => RuleFor(x => x.SucursalId).GreaterThan(0).WithMessage("Seleccione la sucursal.");
}

public sealed class CajaReglas(IReferencias referencias) : IReglasCatalogo<CajaDto, CrearCajaDto, EditarCajaDto>
{
    public async Task ValidarCrearAsync(long empresaId, CrearCajaDto dto, CancellationToken ct)
    {
        if (!await referencias.ExisteActivaAsync(EntidadReferencia.Sucursal, dto.SucursalId, empresaId, ct))
            throw new ValidacionException("sucursalId", "La sucursal no existe o está inactiva.");

        await ValidarBodegaAsync(empresaId, dto.SucursalId, dto.BodegaId, ct);
    }

    public async Task ValidarEditarAsync(long empresaId, CajaDto anterior, EditarCajaDto dto, CancellationToken ct)
    {
        if (!anterior.Activo && dto.Activo
            && !await referencias.ExisteActivaAsync(EntidadReferencia.Sucursal, anterior.SucursalId, empresaId, ct))
            throw new ReglaNegocioException("SUCURSAL_INACTIVA", "No se puede reactivar la caja: su sucursal está inactiva.");

        if (dto.BodegaId != anterior.BodegaId)
            await ValidarBodegaAsync(empresaId, anterior.SucursalId, dto.BodegaId, ct);
    }

    public Task ValidarDesactivarAsync(long empresaId, CajaDto anterior, CancellationToken ct) => Task.CompletedTask;

    private async Task ValidarBodegaAsync(long empresaId, long sucursalId, long? bodegaId, CancellationToken ct)
    {
        if (bodegaId is null)
            return;

        if (!await referencias.ExisteActivaAsync(EntidadReferencia.Bodega, bodegaId.Value, empresaId, ct))
            throw new ValidacionException("bodegaId", "La bodega no existe o está inactiva.");

        if (await referencias.SucursalDeAsync(EntidadReferencia.Bodega, bodegaId.Value, empresaId, ct) != sucursalId)
            throw new ValidacionException("bodegaId", "La bodega debe pertenecer a la misma sucursal que la caja.");
    }
}

// ----- Departamentos -----

public sealed record DepartamentoDto : CatalogoDto;

public sealed record CrearDepartamentoDto : CrearCatalogo;

public sealed record EditarDepartamentoDto : EditarCatalogo;

public sealed class DepartamentoReglas(IReferencias referencias) : IReglasCatalogo<DepartamentoDto, CrearDepartamentoDto, EditarDepartamentoDto>
{
    public Task ValidarCrearAsync(long empresaId, CrearDepartamentoDto dto, CancellationToken ct) => Task.CompletedTask;

    public Task ValidarEditarAsync(long empresaId, DepartamentoDto anterior, EditarDepartamentoDto dto, CancellationToken ct) => Task.CompletedTask;

    public async Task ValidarDesactivarAsync(long empresaId, DepartamentoDto anterior, CancellationToken ct)
    {
        if (await referencias.ContarDependientesActivosAsync(RelacionDependiente.CentrosCostoDeDepartamento, anterior.Id, ct) > 0)
            throw new ReglaNegocioException("TIENE_DEPENDIENTES", "El departamento tiene centros de costo activos. Desactívelos primero.");
    }
}

// ----- Centros de costo -----

public sealed record CentroCostoDto : CatalogoDto
{
    public long? DepartamentoId { get; init; }
    public string? DepartamentoNombre { get; init; }
}

public sealed record CrearCentroCostoDto : CrearCatalogo
{
    public long? DepartamentoId { get; init; }
}

public sealed record EditarCentroCostoDto : EditarCatalogo
{
    public long? DepartamentoId { get; init; }
}

public sealed class CentroCostoReglas(IReferencias referencias) : IReglasCatalogo<CentroCostoDto, CrearCentroCostoDto, EditarCentroCostoDto>
{
    public Task ValidarCrearAsync(long empresaId, CrearCentroCostoDto dto, CancellationToken ct) =>
        ValidarDepartamentoAsync(empresaId, dto.DepartamentoId, ct);

    public async Task ValidarEditarAsync(long empresaId, CentroCostoDto anterior, EditarCentroCostoDto dto, CancellationToken ct)
    {
        // Solo se revisa si cambia: un centro puede seguir apuntando a un departamento ya inactivo.
        if (dto.DepartamentoId != anterior.DepartamentoId)
            await ValidarDepartamentoAsync(empresaId, dto.DepartamentoId, ct);
    }

    public Task ValidarDesactivarAsync(long empresaId, CentroCostoDto anterior, CancellationToken ct) => Task.CompletedTask;

    private async Task ValidarDepartamentoAsync(long empresaId, long? departamentoId, CancellationToken ct)
    {
        if (departamentoId is not null
            && !await referencias.ExisteActivaAsync(EntidadReferencia.Departamento, departamentoId.Value, empresaId, ct))
            throw new ValidacionException("departamentoId", "El departamento no existe o está inactivo.");
    }
}
