using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Catalogos;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Catalogos;

// ===== Tipo de cambio =====

public sealed record TipoCambioDto
{
    public long Id { get; init; }
    public long MonedaId { get; init; }
    public string MonedaCodigo { get; init; } = "";
    public DateOnly Fecha { get; init; }
    public decimal Tasa { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }
}

public sealed record CrearTipoCambioDto(long MonedaId, DateOnly Fecha, decimal Tasa);

public sealed record EditarTipoCambioDto(decimal Tasa, uint VersionFila);

public sealed record ConsultaTiposCambio : ConsultaPaginada
{
    public long? MonedaId { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
}

/// <summary>Tasa aplicable a una fecha: la propia de ese día o la última anterior.</summary>
public sealed record TasaVigente
{
    public long MonedaId { get; init; }
    public DateOnly FechaTasa { get; init; }
    public decimal Tasa { get; init; }
}

/// <summary>Contrato hacia otros módulos (documentos en moneda extranjera).</summary>
public interface ITiposCambioConsulta
{
    /// <summary>
    /// Tasa de la moneda a la fecha dada. Para la moneda local devuelve 1. Nulo si no hay ninguna tasa
    /// registrada hasta esa fecha: quien llama debe pedir al usuario que la registre.
    /// </summary>
    Task<TasaVigente?> ObtenerVigenteAsync(long empresaId, long monedaId, DateOnly fecha, CancellationToken ct);
}

public interface ITipoCambioRepository : ITiposCambioConsulta
{
    Task<ResultadoPaginado<TipoCambioDto>> ListarAsync(long empresaId, ConsultaTiposCambio consulta, CancellationToken ct);

    Task<IReadOnlyList<TipoCambioDto>> ListarParaExportarAsync(long empresaId, ConsultaTiposCambio consulta, int maximo, CancellationToken ct);

    Task<TipoCambioDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    Task<long?> MonedaLocalIdAsync(long empresaId, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, CrearTipoCambioDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long empresaId, long id, EditarTipoCambioDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

public sealed class CrearTipoCambioValidator : AbstractValidator<CrearTipoCambioDto>
{
    public CrearTipoCambioValidator()
    {
        RuleFor(x => x.MonedaId).GreaterThan(0).WithMessage("Seleccione la moneda.");
        RuleFor(x => x.Fecha).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
        RuleFor(x => x.Tasa).TasaValida();
    }
}

public sealed class EditarTipoCambioValidator : AbstractValidator<EditarTipoCambioDto>
{
    public EditarTipoCambioValidator()
    {
        RuleFor(x => x.Tasa).TasaValida();
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

internal static class ReglasDeTasa
{
    /// <summary>DECIMAL(18,8): hasta 8 decimales y 10 enteros.</summary>
    public static IRuleBuilderOptions<T, decimal> TasaValida<T>(this IRuleBuilder<T, decimal> regla) =>
        regla
            .GreaterThan(0m).WithMessage("La tasa debe ser mayor que cero.")
            .LessThan(10_000_000_000m).WithMessage("La tasa es demasiado grande.")
            .Must(t => decimal.Round(t, 8) == t).WithMessage("La tasa admite hasta 8 decimales.");
}

public sealed class TiposCambioService(
    ITipoCambioRepository repositorio,
    IReferencias referencias,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    public const int MaximoExportacion = 10_000;
    private const string Modulo = "CATALOGOS";
    private const string Entidad = "tipo_cambio";

    public Task<ResultadoPaginado<TipoCambioDto>> ListarAsync(ConsultaTiposCambio consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<TipoCambioDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
        ?? throw new NoEncontradoException("El tipo de cambio no existe.");

    public Task<TipoCambioDto> CrearAsync(CrearTipoCambioDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        // Los bancos publican la tasa del día siguiente; más allá no hay dato real.
        var limite = FechaNegocio.Hoy(reloj.UtcNow).AddDays(1);
        if (dto.Fecha > limite)
            throw new ValidacionException("fecha", "La fecha no puede ser posterior a mañana.");

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (!await referencias.ExisteActivaAsync(EntidadReferencia.Moneda, dto.MonedaId, empresaId, ct))
                throw new ValidacionException("monedaId", "La moneda no existe o está inactiva.");

            if (dto.MonedaId == await repositorio.MonedaLocalIdAsync(empresaId, ct))
                throw new ReglaNegocioException("MONEDA_LOCAL", "La moneda local no lleva tipo de cambio.");

            // La unicidad (empresa, moneda, fecha) la garantiza la base: el repositorio traduce el 1062 a 409.
            var id = await repositorio.InsertarAsync(empresaId, dto, usuarioId, reloj.UtcNow, ct);
            var creado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creado), ct);
            return creado;
        }, ct);
    }

    public Task<TipoCambioDto> EditarAsync(long id, EditarTipoCambioDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(empresaId, id, ct)
                ?? throw new NoEncontradoException("El tipo de cambio no existe.");

            if (!await repositorio.ActualizarAsync(empresaId, id, dto, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "EDITAR", anterior, actualizado), ct);
            return actualizado;
        }, ct);
    }

    public async Task<IReadOnlyList<TipoCambioDto>> ExportarAsync(ConsultaTiposCambio consulta, CancellationToken ct)
    {
        var filas = await repositorio.ListarParaExportarAsync(actual.EmpresaIdRequerida(), consulta, MaximoExportacion, ct);
        await auditoria.RegistrarAsync(
            new RegistroAuditoria(Modulo, Entidad, null, "EXPORTAR", Mensaje: $"{filas.Count} filas exportadas"), ct);
        return filas;
    }
}

// ===== Impuestos =====

public static class TiposImpuesto
{
    public static readonly IReadOnlyList<string> Todos = ["IVA", "OTRO"];
}

public sealed record ImpuestoDto
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Tipo { get; init; } = "";
    public decimal Porcentaje { get; init; }
    public DateOnly VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }

    public bool EstaVigente(DateOnly fecha) => Activo && Vigencias.Contiene(VigenteDesde, VigenteHasta, fecha);
}

public sealed record CrearImpuestoDto(string Codigo, string Nombre, string Tipo, decimal Porcentaje, DateOnly VigenteDesde, DateOnly? VigenteHasta);

/// <summary>El porcentaje, el tipo y el inicio no se editan: un cambio de tasa es una vigencia nueva.</summary>
public sealed record EditarImpuestoDto(string Nombre, DateOnly? VigenteHasta, bool Activo, uint VersionFila);

public sealed record ConsultaImpuestos : ConsultaPaginada
{
    public bool? Activo { get; init; }

    /// <summary>Solo los vigentes a esta fecha.</summary>
    public DateOnly? VigentesA { get; init; }
}

public sealed class CrearImpuestoValidator : AbstractValidator<CrearImpuestoDto>
{
    public CrearImpuestoValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(20)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("El código solo admite letras, números, guion y guion bajo.");
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Tipo).Must(t => TiposImpuesto.Todos.Contains(t)).WithMessage("Tipo de impuesto no válido.");
        RuleFor(x => x.Porcentaje).InclusiveBetween(0m, 100m)
            .Must(p => decimal.Round(p, 6) == p).WithMessage("El porcentaje admite hasta 6 decimales.");
        RuleFor(x => x.VigenteDesde).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.VigenteHasta).GreaterThanOrEqualTo(x => x.VigenteDesde)
            .When(x => x.VigenteHasta is not null).WithMessage("La vigencia no puede terminar antes de empezar.");
    }
}

public sealed class EditarImpuestoValidator : AbstractValidator<EditarImpuestoDto>
{
    public EditarImpuestoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

/// <summary>Vigencia de otro registro con el mismo código.</summary>
public sealed record VigenciaImpuesto
{
    public long Id { get; init; }
    public DateOnly VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
}

public interface IImpuestoRepository
{
    Task<ResultadoPaginado<ImpuestoDto>> ListarAsync(long empresaId, ConsultaImpuestos consulta, CancellationToken ct);

    Task<IReadOnlyList<ImpuestoDto>> ListarParaExportarAsync(long empresaId, ConsultaImpuestos consulta, int maximo, CancellationToken ct);

    Task<ImpuestoDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    /// <summary>Vigencias activas del código (con bloqueo de filas dentro de una transacción).</summary>
    Task<IReadOnlyList<VigenciaImpuesto>> VigenciasActivasAsync(long empresaId, string codigo, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, CrearImpuestoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long empresaId, long id, EditarImpuestoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

public sealed class ImpuestosService(
    IImpuestoRepository repositorio,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    public const int MaximoExportacion = 10_000;
    private const string Modulo = "CATALOGOS";
    private const string Entidad = "impuesto";

    public Task<ResultadoPaginado<ImpuestoDto>> ListarAsync(ConsultaImpuestos consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<ImpuestoDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
        ?? throw new NoEncontradoException("El impuesto no existe.");

    public Task<ImpuestoDto> CrearAsync(CrearImpuestoDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with { Codigo = dto.Codigo.Trim().ToUpperInvariant(), Nombre = dto.Nombre.Trim() };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var vigencias = await repositorio.VigenciasActivasAsync(empresaId, limpio.Codigo, ct);
            if (vigencias.Any(v => v.VigenteDesde == limpio.VigenteDesde))
                throw new ConflictoException("DUPLICADO", $"Ya existe una vigencia de {limpio.Codigo} que inicia en esa fecha.");

            VerificarSinTraslape(vigencias, 0, limpio.VigenteDesde, limpio.VigenteHasta);

            var id = await repositorio.InsertarAsync(empresaId, limpio, usuarioId, reloj.UtcNow, ct);
            var creado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creado), ct);
            return creado;
        }, ct);
    }

    public Task<ImpuestoDto> EditarAsync(long id, EditarImpuestoDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with { Nombre = dto.Nombre.Trim() };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(empresaId, id, ct)
                ?? throw new NoEncontradoException("El impuesto no existe.");

            if (limpio.VigenteHasta is not null && limpio.VigenteHasta < anterior.VigenteDesde)
                throw new ValidacionException("vigenteHasta", "La vigencia no puede terminar antes de empezar.");

            // Solo importa el traslape si el registro queda activo.
            if (limpio.Activo)
            {
                var vigencias = await repositorio.VigenciasActivasAsync(empresaId, anterior.Codigo, ct);
                VerificarSinTraslape(vigencias, id, anterior.VigenteDesde, limpio.VigenteHasta);
            }

            if (!await repositorio.ActualizarAsync(empresaId, id, limpio, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizado = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            var accion = anterior.Activo && !actualizado.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizado), ct);
            return actualizado;
        }, ct);
    }

    /// <summary>Baja lógica de una vigencia: queda inactiva y su código/inicio siguen reservados.</summary>
    public Task DesactivarAsync(long id, uint versionFila, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var existente = await ObtenerAsync(id, ct);
            if (existente.Activo)
                await EditarAsync(id, new EditarImpuestoDto(existente.Nombre, existente.VigenteHasta, false, versionFila), ct);
        }, ct);

    public async Task<IReadOnlyList<ImpuestoDto>> ExportarAsync(ConsultaImpuestos consulta, CancellationToken ct)
    {
        var filas = await repositorio.ListarParaExportarAsync(actual.EmpresaIdRequerida(), consulta, MaximoExportacion, ct);
        await auditoria.RegistrarAsync(
            new RegistroAuditoria(Modulo, Entidad, null, "EXPORTAR", Mensaje: $"{filas.Count} filas exportadas"), ct);
        return filas;
    }

    private static void VerificarSinTraslape(IReadOnlyList<VigenciaImpuesto> vigencias, long idPropio, DateOnly desde, DateOnly? hasta)
    {
        var choque = vigencias.FirstOrDefault(v => v.Id != idPropio && Vigencias.SeTraslapan(desde, hasta, v.VigenteDesde, v.VigenteHasta));
        if (choque is not null)
            throw new ReglaNegocioException("VIGENCIA_TRASLAPADA",
                "La vigencia se traslapa con otra del mismo impuesto. Cierre primero la vigencia anterior " +
                $"(vigente desde {choque.VigenteDesde:dd/MM/yyyy}).");
    }
}
