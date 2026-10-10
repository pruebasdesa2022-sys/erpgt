using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Catalogos;

// Catálogos de la etapa 1A sobre el motor genérico. Los que no tienen datos propios solo
// declaran sus tres tipos; los validadores base se registran solos (ver RegistroCatalogos).

// ----- Productos -----

public sealed record TipoProductoDto : CatalogoDto
{
    /// <summary>Los servicios no manejan inventario.</summary>
    public bool ManejaInventario { get; init; }
}

public sealed record CrearTipoProductoDto : CrearCatalogo
{
    public bool ManejaInventario { get; init; } = true;
}

public sealed record EditarTipoProductoDto : EditarCatalogo
{
    public bool ManejaInventario { get; init; } = true;
}

public sealed record CategoriaDto : CatalogoDto;

public sealed record CrearCategoriaDto : CrearCatalogo;

public sealed record EditarCategoriaDto : EditarCatalogo;

public sealed record MarcaDto : CatalogoDto;

public sealed record CrearMarcaDto : CrearCatalogo;

public sealed record EditarMarcaDto : EditarCatalogo;

// ----- Unidades de medida -----

public static class Magnitudes
{
    public static readonly IReadOnlyList<string> Todas = ["CANTIDAD", "PESO", "VOLUMEN", "LONGITUD"];
}

public sealed record UnidadMedidaDto : CatalogoDto
{
    public string Magnitud { get; init; } = "";
    public byte Decimales { get; init; }
}

public sealed record CrearUnidadMedidaDto : CrearCatalogo
{
    public string Magnitud { get; init; } = "CANTIDAD";
    public byte Decimales { get; init; }
}

/// <summary>La magnitud no cambia: las conversiones existentes dependen de ella.</summary>
public sealed record EditarUnidadMedidaDto : EditarCatalogo
{
    public byte Decimales { get; init; }
}

public sealed class CrearUnidadMedidaValidator : AbstractValidator<CrearUnidadMedidaDto>
{
    public CrearUnidadMedidaValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(10)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo admite letras, números, guion y guion bajo.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Magnitud).Must(m => Magnitudes.Todas.Contains(m)).WithMessage("Magnitud no válida.");
        RuleFor(x => x.Decimales).LessThanOrEqualTo((byte)6);
    }
}

public sealed class EditarUnidadMedidaValidator : AbstractValidator<EditarUnidadMedidaDto>
{
    public EditarUnidadMedidaValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Decimales).LessThanOrEqualTo((byte)6);
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

// ----- Comercial -----

public sealed record TipoProveedorDto : CatalogoDto;

public sealed record CrearTipoProveedorDto : CrearCatalogo;

public sealed record EditarTipoProveedorDto : EditarCatalogo;

public sealed record MotivoAnulacionDto : CatalogoDto;

public sealed record CrearMotivoAnulacionDto : CrearCatalogo;

public sealed record EditarMotivoAnulacionDto : EditarCatalogo;

public static class TiposFormaPago
{
    public static readonly IReadOnlyList<string> Todos = ["EFECTIVO", "BANCARIO", "TARJETA", "CREDITO"];
}

public sealed record FormaPagoDto : CatalogoDto
{
    public string Tipo { get; init; } = "";
    public bool RequiereReferencia { get; init; }
}

public sealed record CrearFormaPagoDto : CrearCatalogo
{
    public string Tipo { get; init; } = "EFECTIVO";
    public bool RequiereReferencia { get; init; }
}

public sealed record EditarFormaPagoDto : EditarCatalogo
{
    public string Tipo { get; init; } = "EFECTIVO";
    public bool RequiereReferencia { get; init; }
}

public sealed class CrearFormaPagoValidator : CrearCatalogoValidator<CrearFormaPagoDto>
{
    public CrearFormaPagoValidator() =>
        RuleFor(x => x.Tipo).Must(t => TiposFormaPago.Todos.Contains(t)).WithMessage("Tipo de forma de pago no válido.");
}

public sealed class EditarFormaPagoValidator : EditarCatalogoValidator<EditarFormaPagoDto>
{
    public EditarFormaPagoValidator() =>
        RuleFor(x => x.Tipo).Must(t => TiposFormaPago.Todos.Contains(t)).WithMessage("Tipo de forma de pago no válido.");
}

public sealed record CondicionPagoDto : CatalogoDto
{
    public int DiasCredito { get; init; }
}

public sealed record CrearCondicionPagoDto : CrearCatalogo
{
    public int DiasCredito { get; init; }
}

public sealed record EditarCondicionPagoDto : EditarCatalogo
{
    public int DiasCredito { get; init; }
}

public sealed class CrearCondicionPagoValidator : CrearCatalogoValidator<CrearCondicionPagoDto>
{
    public CrearCondicionPagoValidator() => RuleFor(x => x.DiasCredito).InclusiveBetween(0, 365);
}

public sealed class EditarCondicionPagoValidator : EditarCatalogoValidator<EditarCondicionPagoDto>
{
    public EditarCondicionPagoValidator() => RuleFor(x => x.DiasCredito).InclusiveBetween(0, 365);
}

// ----- Finanzas -----

/// <summary>Catálogo global: lo comparten todas las empresas.</summary>
public sealed record MonedaDto : CatalogoDto
{
    public string Simbolo { get; init; } = "";
    public byte Decimales { get; init; }
}

public sealed record CrearMonedaDto : CrearCatalogo
{
    public string Simbolo { get; init; } = "";
    public byte Decimales { get; init; } = 2;
}

public sealed record EditarMonedaDto : EditarCatalogo
{
    public string Simbolo { get; init; } = "";
    public byte Decimales { get; init; } = 2;
}

public sealed class CrearMonedaValidator : AbstractValidator<CrearMonedaDto>
{
    public CrearMonedaValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("El código es de 3 letras (ISO 4217).");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Simbolo).NotEmpty().MaximumLength(5);
        RuleFor(x => x.Decimales).LessThanOrEqualTo((byte)6);
    }
}

public sealed class EditarMonedaValidator : AbstractValidator<EditarMonedaDto>
{
    public EditarMonedaValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Simbolo).NotEmpty().MaximumLength(5);
        RuleFor(x => x.Decimales).LessThanOrEqualTo((byte)6);
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

public sealed class MonedaReglas(IReferencias referencias) : IReglasCatalogo<MonedaDto, CrearMonedaDto, EditarMonedaDto>
{
    public Task ValidarCrearAsync(long empresaId, CrearMonedaDto dto, CancellationToken ct) => Task.CompletedTask;

    public Task ValidarEditarAsync(long empresaId, MonedaDto anterior, EditarMonedaDto dto, CancellationToken ct) => Task.CompletedTask;

    public async Task ValidarDesactivarAsync(long empresaId, MonedaDto anterior, CancellationToken ct)
    {
        if (await referencias.ContarDependientesActivosAsync(RelacionDependiente.EmpresasDeMonedaLocal, anterior.Id, ct) > 0)
            throw new ReglaNegocioException("MONEDA_LOCAL", "La moneda es la moneda local de una empresa y no se puede desactivar.");

        if (await referencias.ContarDependientesActivosAsync(RelacionDependiente.CuentasDeMoneda, anterior.Id, ct) > 0)
            throw new ReglaNegocioException("TIENE_DEPENDIENTES", "La moneda tiene cuentas bancarias activas.");
    }
}

public sealed record BancoDto : CatalogoDto;

public sealed record CrearBancoDto : CrearCatalogo;

public sealed record EditarBancoDto : EditarCatalogo;

public sealed class BancoReglas(IReferencias referencias) : IReglasCatalogo<BancoDto, CrearBancoDto, EditarBancoDto>
{
    public Task ValidarCrearAsync(long empresaId, CrearBancoDto dto, CancellationToken ct) => Task.CompletedTask;

    public Task ValidarEditarAsync(long empresaId, BancoDto anterior, EditarBancoDto dto, CancellationToken ct) => Task.CompletedTask;

    public async Task ValidarDesactivarAsync(long empresaId, BancoDto anterior, CancellationToken ct)
    {
        if (await referencias.ContarDependientesActivosAsync(RelacionDependiente.CuentasDeBanco, anterior.Id, ct) > 0)
            throw new ReglaNegocioException("TIENE_DEPENDIENTES", "El banco tiene cuentas bancarias activas.");
    }
}

public static class TiposCuentaBancaria
{
    public static readonly IReadOnlyList<string> Todos = ["MONETARIA", "AHORRO", "OTRA"];
}

public sealed record CuentaBancariaDto : CatalogoDto
{
    public long BancoId { get; init; }
    public string BancoNombre { get; init; } = "";
    public long MonedaId { get; init; }
    public string MonedaCodigo { get; init; } = "";
    public string NumeroCuenta { get; init; } = "";
    public string TipoCuenta { get; init; } = "";
    public string? Titular { get; init; }
}

public sealed record CrearCuentaBancariaDto : CrearCatalogo
{
    public long BancoId { get; init; }
    public long MonedaId { get; init; }
    public string NumeroCuenta { get; init; } = "";
    public string TipoCuenta { get; init; } = "MONETARIA";
    public string? Titular { get; init; }
}

/// <summary>Banco, moneda y número no cambian: los movimientos futuros se registran contra ellos.</summary>
public sealed record EditarCuentaBancariaDto : EditarCatalogo
{
    public string TipoCuenta { get; init; } = "MONETARIA";
    public string? Titular { get; init; }
}

public sealed class CrearCuentaBancariaValidator : CrearCatalogoValidator<CrearCuentaBancariaDto>
{
    public CrearCuentaBancariaValidator()
    {
        RuleFor(x => x.BancoId).GreaterThan(0).WithMessage("Seleccione el banco.");
        RuleFor(x => x.MonedaId).GreaterThan(0).WithMessage("Seleccione la moneda.");
        RuleFor(x => x.NumeroCuenta).NotEmpty().MaximumLength(40);
        RuleFor(x => x.TipoCuenta).Must(t => TiposCuentaBancaria.Todos.Contains(t)).WithMessage("Tipo de cuenta no válido.");
        RuleFor(x => x.Titular).MaximumLength(150);
    }
}

public sealed class EditarCuentaBancariaValidator : EditarCatalogoValidator<EditarCuentaBancariaDto>
{
    public EditarCuentaBancariaValidator()
    {
        RuleFor(x => x.TipoCuenta).Must(t => TiposCuentaBancaria.Todos.Contains(t)).WithMessage("Tipo de cuenta no válido.");
        RuleFor(x => x.Titular).MaximumLength(150);
    }
}

public sealed class CuentaBancariaReglas(IReferencias referencias)
    : IReglasCatalogo<CuentaBancariaDto, CrearCuentaBancariaDto, EditarCuentaBancariaDto>
{
    public async Task ValidarCrearAsync(long empresaId, CrearCuentaBancariaDto dto, CancellationToken ct)
    {
        if (!await referencias.ExisteActivaAsync(EntidadReferencia.Banco, dto.BancoId, empresaId, ct))
            throw new ValidacionException("bancoId", "El banco no existe o está inactivo.");

        if (!await referencias.ExisteActivaAsync(EntidadReferencia.Moneda, dto.MonedaId, empresaId, ct))
            throw new ValidacionException("monedaId", "La moneda no existe o está inactiva.");
    }

    public Task ValidarEditarAsync(long empresaId, CuentaBancariaDto anterior, EditarCuentaBancariaDto dto, CancellationToken ct) =>
        Task.CompletedTask;

    public Task ValidarDesactivarAsync(long empresaId, CuentaBancariaDto anterior, CancellationToken ct) => Task.CompletedTask;
}

// ----- Documentos -----

/// <summary>
/// Catálogo global y cerrado: cada código implica un comportamiento del sistema, así que la aplicación
/// no crea tipos nuevos; solo permite cambiar el nombre y el estado.
/// </summary>
public sealed record TipoDocumentoDto : CatalogoDto
{
    public string Modulo { get; init; } = "";
}

public sealed record CrearTipoDocumentoDto : CrearCatalogo;

public sealed record EditarTipoDocumentoDto : EditarCatalogo;
