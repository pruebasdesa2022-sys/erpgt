using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Catalogos;
using ERP.Domain.Comun;
using FluentValidation;

namespace ERP.Application.Catalogos;

// ===== Conversiones de unidades =====

public sealed record ConversionUnidadDto
{
    public long Id { get; init; }
    public long? EmpresaId { get; init; }
    public long UnidadOrigenId { get; init; }
    public string UnidadOrigenCodigo { get; init; } = "";
    public long UnidadDestinoId { get; init; }
    public string UnidadDestinoCodigo { get; init; } = "";
    public string Magnitud { get; init; } = "";

    /// <summary>1 unidad de origen = Factor unidades de destino.</summary>
    public decimal Factor { get; init; }

    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }
}

public sealed record CrearConversionUnidadDto(long UnidadOrigenId, long UnidadDestinoId, decimal Factor);

public sealed record EditarConversionUnidadDto(decimal Factor, bool Activo, uint VersionFila);

public sealed record ConsultaConversiones : ConsultaPaginada
{
    public bool? Activo { get; init; }
}

public sealed class CrearConversionUnidadValidator : AbstractValidator<CrearConversionUnidadDto>
{
    public CrearConversionUnidadValidator()
    {
        RuleFor(x => x.UnidadOrigenId).GreaterThan(0).WithMessage("Seleccione la unidad de origen.");
        RuleFor(x => x.UnidadDestinoId).GreaterThan(0).WithMessage("Seleccione la unidad de destino.");
        RuleFor(x => x.UnidadDestinoId).NotEqual(x => x.UnidadOrigenId).WithMessage("Las unidades deben ser distintas.");
        RuleFor(x => x.Factor).FactorValido();
    }
}

public sealed class EditarConversionUnidadValidator : AbstractValidator<EditarConversionUnidadDto>
{
    public EditarConversionUnidadValidator()
    {
        RuleFor(x => x.Factor).FactorValido();
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

internal static class ReglasDeFactor
{
    /// <summary>DECIMAL(18,6): hasta 6 decimales y 12 enteros.</summary>
    public static IRuleBuilderOptions<T, decimal> FactorValido<T>(this IRuleBuilder<T, decimal> regla) =>
        regla
            .GreaterThan(0m).WithMessage("El factor debe ser mayor que cero.")
            .LessThan(1_000_000_000_000m).WithMessage("El factor es demasiado grande.")
            .Must(f => decimal.Round(f, 6) == f).WithMessage("El factor admite hasta 6 decimales.");
}

public interface IConversionUnidadRepository
{
    Task<ResultadoPaginado<ConversionUnidadDto>> ListarAsync(long empresaId, ConsultaConversiones consulta, CancellationToken ct);

    Task<ConversionUnidadDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    /// <summary>Conversiones activas visibles para la empresa (estándar y propias).</summary>
    Task<IReadOnlyList<ConversionUnidadDto>> ListarActivasAsync(long empresaId, CancellationToken ct);

    /// <summary>La unidad (estándar o propia) si está activa.</summary>
    Task<UnidadBasica?> ObtenerUnidadActivaAsync(long empresaId, long unidadId, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, CrearConversionUnidadDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long empresaId, long id, EditarConversionUnidadDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

public sealed record UnidadBasica
{
    public long Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Magnitud { get; init; } = "";
}

public sealed class ConversionesUnidadService(
    IConversionUnidadRepository repositorio,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    private const string Modulo = "CATALOGOS";
    private const string Entidad = "unidad_conversion";

    public Task<ResultadoPaginado<ConversionUnidadDto>> ListarAsync(ConsultaConversiones consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<ConversionUnidadDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
        ?? throw new NoEncontradoException("La conversión no existe.");

    public Task<ConversionUnidadDto> CrearAsync(CrearConversionUnidadDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var origen = await repositorio.ObtenerUnidadActivaAsync(empresaId, dto.UnidadOrigenId, ct)
                ?? throw new ValidacionException("unidadOrigenId", "La unidad no existe o está inactiva.");
            var destino = await repositorio.ObtenerUnidadActivaAsync(empresaId, dto.UnidadDestinoId, ct)
                ?? throw new ValidacionException("unidadDestinoId", "La unidad no existe o está inactiva.");

            if (origen.Magnitud != destino.Magnitud)
                throw new ReglaNegocioException("MAGNITUD_DISTINTA",
                    $"No se puede convertir entre {origen.Magnitud.ToLowerInvariant()} y {destino.Magnitud.ToLowerInvariant()}.");

            await VerificarSinRedundanciaAsync(empresaId, 0, dto.UnidadOrigenId, dto.UnidadDestinoId, ct);

            var id = await repositorio.InsertarAsync(empresaId, dto, usuarioId, reloj.UtcNow, ct);
            var creada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creada), ct);
            return creada;
        }, ct);
    }

    public Task<ConversionUnidadDto> EditarAsync(long id, EditarConversionUnidadDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(empresaId, id, ct)
                ?? throw new NoEncontradoException("La conversión no existe.");

            if (anterior.EmpresaId is null)
                throw new ReglaNegocioException("ESTANDAR_SOLO_LECTURA", "La conversión es estándar del sistema y no se puede modificar.");

            // Reactivar vuelve a unir dos unidades: se revisa igual que una conversión nueva.
            if (!anterior.Activo && dto.Activo)
                await VerificarSinRedundanciaAsync(empresaId, id, anterior.UnidadOrigenId, anterior.UnidadDestinoId, ct);

            if (!await repositorio.ActualizarAsync(empresaId, id, dto, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            var accion = anterior.Activo && !actualizada.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizada), ct);
            return actualizada;
        }, ct);
    }

    public Task DesactivarAsync(long id, uint versionFila, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var existente = await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
                ?? throw new NoEncontradoException("La conversión no existe.");
            if (existente.Activo)
                await EditarAsync(id, new EditarConversionUnidadDto(existente.Factor, false, versionFila), ct);
        }, ct);

    /// <summary>
    /// Si las dos unidades ya están conectadas (directa o indirectamente), una conversión más sería un
    /// ciclo o un segundo camino con un factor que podría contradecir al primero.
    /// </summary>
    private async Task VerificarSinRedundanciaAsync(long empresaId, long idPropio, long origenId, long destinoId, CancellationToken ct)
    {
        var existentes = (await repositorio.ListarActivasAsync(empresaId, ct)).Where(c => c.Id != idPropio);
        var grafo = new GrafoConversiones(existentes.Select(c => new AristaConversion(c.UnidadOrigenId, c.UnidadDestinoId, c.Factor)));

        if (grafo.EstanConectadas(origenId, destinoId))
            throw new ReglaNegocioException("CONVERSION_REDUNDANTE",
                "Las dos unidades ya se pueden convertir entre sí con las conversiones existentes.");
    }
}

// ===== Series y correlativos =====

public sealed record SerieDocumentoDto
{
    public long Id { get; init; }
    public long TipoDocumentoId { get; init; }
    public string TipoDocumentoCodigo { get; init; } = "";
    public string TipoDocumentoNombre { get; init; } = "";
    public long? SucursalId { get; init; }
    public string? SucursalNombre { get; init; }
    public long? CajaId { get; init; }
    public string? CajaNombre { get; init; }
    public string Serie { get; init; } = "";
    public string? Prefijo { get; init; }
    public bool IncluirAnio { get; init; }
    public int Longitud { get; init; }

    /// <summary>Número que recibirá el próximo documento.</summary>
    public long SiguienteCorrelativo { get; init; }

    public long? NumeroMaximo { get; init; }
    public DateOnly VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
    public bool Activo { get; init; }
    public uint VersionFila { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? ModificadoEn { get; init; }
}

public sealed record CrearSerieDocumentoDto
{
    public long TipoDocumentoId { get; init; }
    public long? SucursalId { get; init; }
    public long? CajaId { get; init; }
    public string Serie { get; init; } = "";
    public string? Prefijo { get; init; }
    public bool IncluirAnio { get; init; }
    public int Longitud { get; init; } = 6;

    /// <summary>Número inicial; después solo avanza al emitir documentos.</summary>
    public long SiguienteCorrelativo { get; init; } = 1;

    public long? NumeroMaximo { get; init; }
    public DateOnly VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
}

/// <summary>
/// Formato, serie y numeración en curso no se editan: cambiarlos alteraría cómo se ven o se
/// numeran los documentos ya emitidos.
/// </summary>
public sealed record EditarSerieDocumentoDto(long? NumeroMaximo, DateOnly? VigenteHasta, bool Activo, uint VersionFila);

public sealed record ConsultaSeries : ConsultaPaginada
{
    public bool? Activo { get; init; }
    public long? TipoDocumentoId { get; init; }
}

public sealed class CrearSerieDocumentoValidator : AbstractValidator<CrearSerieDocumentoDto>
{
    public CrearSerieDocumentoValidator()
    {
        RuleFor(x => x.TipoDocumentoId).GreaterThan(0).WithMessage("Seleccione el tipo de documento.");
        RuleFor(x => x.Serie).NotEmpty().MaximumLength(10)
            .Matches("^[A-Za-z0-9]+$").WithMessage("La serie solo admite letras y números.");
        RuleFor(x => x.Prefijo).MaximumLength(10)
            .Matches("^[A-Za-z0-9]+$").When(x => !string.IsNullOrWhiteSpace(x.Prefijo))
            .WithMessage("El prefijo solo admite letras y números.");
        RuleFor(x => x.Longitud).InclusiveBetween(1, 12);
        RuleFor(x => x.SiguienteCorrelativo).GreaterThanOrEqualTo(1);
        RuleFor(x => x.NumeroMaximo).GreaterThanOrEqualTo(x => x.SiguienteCorrelativo)
            .When(x => x.NumeroMaximo is not null).WithMessage("El número máximo no puede ser menor que el inicial.");
        RuleFor(x => x.VigenteDesde).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.VigenteHasta).GreaterThanOrEqualTo(x => x.VigenteDesde)
            .When(x => x.VigenteHasta is not null).WithMessage("La vigencia no puede terminar antes de empezar.");
        RuleFor(x => x.CajaId).Must((dto, caja) => caja is null || dto.SucursalId is not null)
            .WithMessage("Para una serie de caja indique también la sucursal.");
    }
}

public sealed class EditarSerieDocumentoValidator : AbstractValidator<EditarSerieDocumentoDto>
{
    public EditarSerieDocumentoValidator()
    {
        RuleFor(x => x.NumeroMaximo).GreaterThanOrEqualTo(1).When(x => x.NumeroMaximo is not null);
        RuleFor(x => x.VersionFila).GreaterThan(0u);
    }
}

public interface ISerieDocumentoRepository
{
    Task<ResultadoPaginado<SerieDocumentoDto>> ListarAsync(long empresaId, ConsultaSeries consulta, CancellationToken ct);

    Task<SerieDocumentoDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct);

    Task<long> InsertarAsync(long empresaId, CrearSerieDocumentoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);

    Task<bool> ActualizarAsync(long empresaId, long id, EditarSerieDocumentoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

public sealed class SeriesDocumentoService(
    ISerieDocumentoRepository repositorio,
    IReferencias referencias,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj)
{
    private const string Modulo = "CATALOGOS";
    private const string Entidad = "serie_documento";

    public Task<ResultadoPaginado<SerieDocumentoDto>> ListarAsync(ConsultaSeries consulta, CancellationToken ct) =>
        repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

    public async Task<SerieDocumentoDto> ObtenerAsync(long id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(actual.EmpresaIdRequerida(), id, ct)
        ?? throw new NoEncontradoException("La serie no existe.");

    public Task<SerieDocumentoDto> CrearAsync(CrearSerieDocumentoDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var limpio = dto with
        {
            Serie = dto.Serie.Trim().ToUpperInvariant(),
            Prefijo = string.IsNullOrWhiteSpace(dto.Prefijo) ? null : dto.Prefijo.Trim().ToUpperInvariant(),
        };

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (!await referencias.ExisteActivaAsync(EntidadReferencia.TipoDocumento, limpio.TipoDocumentoId, empresaId, ct))
                throw new ValidacionException("tipoDocumentoId", "El tipo de documento no existe o está inactivo.");

            if (limpio.SucursalId is { } sucursalId
                && !await referencias.ExisteActivaAsync(EntidadReferencia.Sucursal, sucursalId, empresaId, ct))
                throw new ValidacionException("sucursalId", "La sucursal no existe o está inactiva.");

            if (limpio.CajaId is { } cajaId)
            {
                if (!await referencias.ExisteActivaAsync(EntidadReferencia.Caja, cajaId, empresaId, ct))
                    throw new ValidacionException("cajaId", "La caja no existe o está inactiva.");

                if (await referencias.SucursalDeAsync(EntidadReferencia.Caja, cajaId, empresaId, ct) != limpio.SucursalId)
                    throw new ValidacionException("cajaId", "La caja no pertenece a la sucursal indicada.");
            }

            // La unicidad (empresa, tipo, serie) la garantiza la base: el repositorio traduce el 1062 a 409.
            var id = await repositorio.InsertarAsync(empresaId, limpio, usuarioId, reloj.UtcNow, ct);
            var creada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), "CREAR", null, creada), ct);
            return creada;
        }, ct);
    }

    public Task<SerieDocumentoDto> EditarAsync(long id, EditarSerieDocumentoDto dto, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = await repositorio.ObtenerAsync(empresaId, id, ct)
                ?? throw new NoEncontradoException("La serie no existe.");

            if (dto.NumeroMaximo is { } maximo && maximo < anterior.SiguienteCorrelativo - 1)
                throw new ReglaNegocioException("MAXIMO_YA_EMITIDO",
                    $"El número máximo no puede ser menor que el último emitido ({anterior.SiguienteCorrelativo - 1}).");

            if (dto.VigenteHasta is { } hasta && hasta < anterior.VigenteDesde)
                throw new ValidacionException("vigenteHasta", "La vigencia no puede terminar antes de empezar.");

            if (!await repositorio.ActualizarAsync(empresaId, id, dto, usuarioId, reloj.UtcNow, ct))
                throw ConflictoException.Concurrencia();

            var actualizada = (await repositorio.ObtenerAsync(empresaId, id, ct))!;
            var accion = anterior.Activo && !actualizada.Activo ? "ELIMINAR" : "EDITAR";
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, id.ToString(), accion, anterior, actualizada), ct);
            return actualizada;
        }, ct);
    }

    public Task DesactivarAsync(long id, uint versionFila, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var existente = await ObtenerAsync(id, ct);
            if (existente.Activo)
                await EditarAsync(id, new EditarSerieDocumentoDto(existente.NumeroMaximo, existente.VigenteHasta, false, versionFila), ct);
        }, ct);
}

// ===== Numeración (contrato para los documentos) =====

public sealed record NumeroAsignado(long SerieId, string Serie, long Numero, string NumeroFormateado);

/// <summary>
/// Lo que el documento que pide el número es: su tipo y dónde se emite. La serie solo sirve si coincide:
/// una serie de otro tipo, de otra sucursal o de otra caja no se puede consumir con solo conocer su id.
/// </summary>
public sealed record AlcanceDocumento(long TipoDocumentoId, long SucursalId, long? CajaId);

public interface INumeracion
{
    /// <summary>
    /// Asigna el siguiente número de la serie, sin saltos ni duplicados bajo concurrencia. Debe llamarse
    /// dentro de la transacción del documento: si el documento se revierte, el número vuelve a la serie.
    /// Rechaza (422) series inactivas, fuera de vigencia a <paramref name="fechaDocumento"/>, agotadas o
    /// que no corresponden al tipo, la sucursal o la caja del documento.
    /// </summary>
    Task<NumeroAsignado> SiguienteAsync(
        long empresaId, long serieId, AlcanceDocumento alcance, DateOnly fechaDocumento, CancellationToken ct);
}

/// <summary>Resultado de <c>sp_siguiente_correlativo</c>: el número asignado y los datos para validarlo y formatearlo.</summary>
public sealed record AsignacionSerie
{
    public long SerieDocumentoId { get; init; }
    public string Serie { get; init; } = "";
    public string? Prefijo { get; init; }
    public bool IncluirAnio { get; init; }
    public int Longitud { get; init; }
    public long? NumeroMaximo { get; init; }
    public DateOnly VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
    public bool Activo { get; init; }
    public long Numero { get; init; }

    // Alcance de la serie: no vienen del procedimiento; el repositorio los completa.
    public long TipoDocumentoId { get; init; }
    public long? SucursalId { get; init; }
    public long? CajaId { get; init; }
}

public interface INumeracionRepository
{
    /// <summary>Llama al procedimiento; nulo si la serie no existe para la empresa.</summary>
    Task<AsignacionSerie?> AvanzarAsync(long empresaId, long serieId, CancellationToken ct);
}

public sealed class NumeracionService(INumeracionRepository repositorio, IUnitOfWork unidadDeTrabajo) : INumeracion
{
    // Dentro de la transacción del documento esto se une a ella; suelto, es atómico por sí mismo.
    public Task<NumeroAsignado> SiguienteAsync(
        long empresaId, long serieId, AlcanceDocumento alcance, DateOnly fechaDocumento, CancellationToken ct) =>
        unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var serie = await repositorio.AvanzarAsync(empresaId, serieId, ct)
                ?? throw new NoEncontradoException("La serie no existe.");

            // Estas validaciones lanzan DESPUÉS de avanzar: el rollback de la transacción devuelve el número.
            if (!serie.Activo)
                throw new ReglaNegocioException("SERIE_INACTIVA", $"La serie {serie.Serie} está inactiva.");

            if (serie.TipoDocumentoId != alcance.TipoDocumentoId)
                throw new ReglaNegocioException("SERIE_TIPO_INCORRECTO", $"La serie {serie.Serie} no es de este tipo de documento.");

            if ((serie.SucursalId is not null && serie.SucursalId != alcance.SucursalId)
                || (serie.CajaId is not null && serie.CajaId != alcance.CajaId))
                throw new ReglaNegocioException("SERIE_FUERA_DE_ALCANCE",
                    $"La serie {serie.Serie} no se puede usar en esta sucursal o caja.");

            if (!Vigencias.Contiene(serie.VigenteDesde, serie.VigenteHasta, fechaDocumento))
                throw new ReglaNegocioException("SERIE_FUERA_DE_VIGENCIA",
                    $"La serie {serie.Serie} no está vigente a la fecha {fechaDocumento:dd/MM/yyyy}.");

            if (serie.NumeroMaximo is { } maximo && serie.Numero > maximo)
                throw new ReglaNegocioException("SERIE_AGOTADA", $"La serie {serie.Serie} llegó a su número máximo ({maximo}).");

            var formateado = FormatoNumeroDocumento.Formatear(
                serie.Serie, serie.Prefijo, serie.IncluirAnio, serie.Longitud, (ulong)serie.Numero, fechaDocumento);
            return new NumeroAsignado(serie.SerieDocumentoId, serie.Serie, serie.Numero, formateado);
        }, ct);
}
