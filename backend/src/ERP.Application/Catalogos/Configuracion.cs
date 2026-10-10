using System.Globalization;
using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Catalogos;
using ERP.Domain.Comun;

namespace ERP.Application.Catalogos;

/// <summary>
/// Parámetro de negocio conocido. La lista vive en código: la pantalla no permite inventar claves, y
/// cada módulo que consume una clave sabe qué tipo y qué valor por defecto esperar.
/// </summary>
/// <param name="Opciones">Valores permitidos para TEXTO; nulo = libre.</param>
public sealed record DefinicionConfiguracion(
    string Clave,
    string Descripcion,
    string TipoDato,
    string? ValorPorDefecto,
    IReadOnlyList<string>? Opciones = null);

public static class ClavesConfiguracion
{
    public const string FechaBloqueo = "FECHA_BLOQUEO";
    public const string StockNegativo = "STOCK_NEGATIVO";
    public const string RedondeoImpuesto = "REDONDEO_IMPUESTO";
    public const string MontoMaximoConsumidorFinal = "MONTO_MAXIMO_CONSUMIDOR_FINAL";
    public const string MetodoCosteo = "METODO_COSTEO";

    public static readonly IReadOnlyList<DefinicionConfiguracion> Todas =
    [
        new(FechaBloqueo,
            "Fecha de bloqueo: no se crean ni anulan documentos con fecha igual o anterior. Vacía = sin bloqueo.",
            "FECHA", null),
        new(StockNegativo, "Permitir que las salidas dejen el inventario en negativo.", "BOOLEANO", "false"),
        new(RedondeoImpuesto, "Dónde se redondea el impuesto: por línea o sobre el total del documento.", "TEXTO", "LINEA", ["LINEA", "DOCUMENTO"]),
        new(MontoMaximoConsumidorFinal,
            "Monto máximo (en moneda local) de una venta a consumidor final sin identificarlo. Confirme el valor con su contador.",
            "DECIMAL", "2500"),
        new(MetodoCosteo, "Método de costeo del inventario.", "TEXTO", "PROMEDIO_PONDERADO", ["PROMEDIO_PONDERADO"]),
    ];

    public static DefinicionConfiguracion? Buscar(string clave) =>
        Todas.FirstOrDefault(d => string.Equals(d.Clave, clave, StringComparison.OrdinalIgnoreCase));
}

public sealed record ConfiguracionDto
{
    public string Clave { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string TipoDato { get; init; } = "";
    public IReadOnlyList<string>? Opciones { get; init; }

    /// <summary>Valor en uso hoy; nulo si no hay ninguno y la clave no tiene valor por defecto.</summary>
    public string? Valor { get; init; }

    public string? ValorPorDefecto { get; init; }

    /// <summary>Verdadero cuando nadie lo ha fijado y rige el valor por defecto.</summary>
    public bool EsPredeterminado { get; init; }

    public DateOnly? VigenteDesde { get; init; }
}

/// <summary>Valor (texto) a fijar; nulo quita el valor propio y vuelve al predeterminado.</summary>
public sealed record EstablecerConfiguracionDto(string? Valor);

public sealed record ValorConfiguracion
{
    public string Clave { get; init; } = "";
    public string Valor { get; init; } = "";
    public DateOnly VigenteDesde { get; init; }
    public bool EsDeEmpresa { get; init; }
}

public interface IConfiguracionRepository
{
    /// <summary>Valores en vigor a una fecha (propios de la empresa y globales).</summary>
    Task<IReadOnlyList<ValorConfiguracion>> VigentesAsync(long empresaId, DateOnly fecha, CancellationToken ct);

    /// <summary>Fija (o quita, con valor nulo) el valor de la empresa desde <paramref name="hoy"/>, conservando el historial.</summary>
    Task EstablecerAsync(long empresaId, string clave, string? valor, string tipoDato, DateOnly hoy, long usuarioId, DateTime ahoraUtc, CancellationToken ct);
}

/// <summary>Contrato hacia otros módulos para leer parámetros de negocio.</summary>
public interface IConfiguracionNegocio
{
    /// <summary>Valor efectivo a la fecha; el predeterminado de la definición si nadie lo fijó (puede ser nulo).</summary>
    Task<string?> ObtenerAsync(long empresaId, string clave, DateOnly fecha, CancellationToken ct);
}

/// <summary>Contrato hacia los documentos: bloqueo de períodos cerrados (<c>fecha_bloqueo</c>, B7).</summary>
public interface IPeriodoBloqueo
{
    /// <summary>
    /// Lanza 422 <c>PERIODO_BLOQUEADO</c> si la fecha del documento es igual o anterior a la fecha de
    /// bloqueo vigente de la empresa. Se aplica al crear y al anular.
    /// </summary>
    Task ValidarFechaAsync(long empresaId, DateOnly fechaDocumento, CancellationToken ct);
}

public sealed class ConfiguracionService(
    IConfiguracionRepository repositorio,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual,
    IClock reloj) : IConfiguracionNegocio, IPeriodoBloqueo
{
    private const string Modulo = "CATALOGOS";
    private const string Entidad = "configuracion";

    public async Task<IReadOnlyList<ConfiguracionDto>> ListarAsync(CancellationToken ct)
    {
        var vigentes = await VigentesPorClaveAsync(actual.EmpresaIdRequerida(), FechaNegocio.Hoy(reloj.UtcNow), ct);
        return ClavesConfiguracion.Todas.Select(d => Armar(d, vigentes.GetValueOrDefault(d.Clave))).ToList();
    }

    public async Task<string?> ObtenerAsync(long empresaId, string clave, DateOnly fecha, CancellationToken ct)
    {
        var definicion = ClavesConfiguracion.Buscar(clave)
            ?? throw new InvalidOperationException($"La clave de configuración {clave} no está definida.");
        var vigentes = await VigentesPorClaveAsync(empresaId, fecha, ct);
        return vigentes.TryGetValue(definicion.Clave, out var valor) ? valor.Valor : definicion.ValorPorDefecto;
    }

    public async Task ValidarFechaAsync(long empresaId, DateOnly fechaDocumento, CancellationToken ct)
    {
        var hoy = FechaNegocio.Hoy(reloj.UtcNow);
        var texto = await ObtenerAsync(empresaId, ClavesConfiguracion.FechaBloqueo, hoy, ct);
        if (texto is null || !DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bloqueo))
            return;

        if (fechaDocumento <= bloqueo)
            throw new ReglaNegocioException("PERIODO_BLOQUEADO",
                $"La fecha {fechaDocumento:dd/MM/yyyy} pertenece a un período cerrado (bloqueado hasta el {bloqueo:dd/MM/yyyy}).");
    }

    /// <summary>Cambio de cualquier parámetro salvo la fecha de bloqueo, que tiene su propio permiso.</summary>
    public Task<ConfiguracionDto> EstablecerAsync(string clave, string? valor, CancellationToken ct)
    {
        var definicion = ClavesConfiguracion.Buscar(clave) ?? throw new NoEncontradoException("El parámetro no existe.");
        if (definicion.Clave == ClavesConfiguracion.FechaBloqueo)
            throw new ReglaNegocioException("USAR_FECHA_BLOQUEO", "La fecha de bloqueo se cambia con su propia opción.");

        return GuardarAsync(definicion, valor, ct);
    }

    public Task<ConfiguracionDto> EstablecerFechaBloqueoAsync(DateOnly? fecha, CancellationToken ct)
    {
        // Un bloqueo en el futuro cerraría documentos de hoy sin que nadie lo haya querido.
        if (fecha is { } f && f > FechaNegocio.Hoy(reloj.UtcNow))
            throw new ValidacionException("valor", "La fecha de bloqueo no puede ser posterior a hoy.");

        var definicion = ClavesConfiguracion.Buscar(ClavesConfiguracion.FechaBloqueo)!;
        return GuardarAsync(definicion, fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);
    }

    private Task<ConfiguracionDto> GuardarAsync(DefinicionConfiguracion definicion, string? valor, CancellationToken ct)
    {
        var empresaId = actual.EmpresaIdRequerida();
        var usuarioId = actual.UsuarioIdRequerido();
        var normalizado = Normalizar(definicion, valor);
        var hoy = FechaNegocio.Hoy(reloj.UtcNow);

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var anterior = (await VigentesPorClaveAsync(empresaId, hoy, ct)).GetValueOrDefault(definicion.Clave);

            await repositorio.EstablecerAsync(empresaId, definicion.Clave, normalizado, definicion.TipoDato, hoy, usuarioId, reloj.UtcNow, ct);

            var nuevo = (await VigentesPorClaveAsync(empresaId, hoy, ct)).GetValueOrDefault(definicion.Clave);
            await auditoria.RegistrarAsync(new RegistroAuditoria(Modulo, Entidad, definicion.Clave, "EDITAR",
                new { clave = definicion.Clave, valor = anterior?.Valor ?? definicion.ValorPorDefecto },
                new { clave = definicion.Clave, valor = nuevo?.Valor ?? definicion.ValorPorDefecto }), ct);

            return Armar(definicion, nuevo);
        }, ct);
    }

    private async Task<Dictionary<string, ValorConfiguracion>> VigentesPorClaveAsync(long empresaId, DateOnly fecha, CancellationToken ct)
    {
        var filas = await repositorio.VigentesAsync(empresaId, fecha, ct);

        // El valor propio de la empresa gana al global; entre varios, el que empezó más tarde.
        return filas
            .GroupBy(f => f.Clave)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.EsDeEmpresa).ThenByDescending(f => f.VigenteDesde).First());
    }

    private static ConfiguracionDto Armar(DefinicionConfiguracion d, ValorConfiguracion? vigente) => new()
    {
        Clave = d.Clave,
        Descripcion = d.Descripcion,
        TipoDato = d.TipoDato,
        Opciones = d.Opciones,
        Valor = vigente?.Valor ?? d.ValorPorDefecto,
        ValorPorDefecto = d.ValorPorDefecto,
        EsPredeterminado = vigente is null,
        VigenteDesde = vigente?.VigenteDesde,
    };

    /// <summary>Valida el valor según el tipo del parámetro y devuelve su forma canónica de almacenamiento.</summary>
    private static string? Normalizar(DefinicionConfiguracion d, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var texto = valor.Trim();
        switch (d.TipoDato)
        {
            case "BOOLEANO":
                return bool.TryParse(texto, out var booleano)
                    ? (booleano ? "true" : "false")
                    : throw new ValidacionException("valor", "Indique verdadero o falso.");

            case "DECIMAL":
                if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) || numero < 0 || numero > 999_999_999_999m)
                    throw new ValidacionException("valor", "Indique un número válido mayor o igual que cero.");
                return numero.ToString(CultureInfo.InvariantCulture);

            case "FECHA":
                return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                    ? fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : throw new ValidacionException("valor", "Indique una fecha válida (AAAA-MM-DD).");

            default:
                if (texto.Length > 2000)
                    throw new ValidacionException("valor", "El valor es demasiado largo.");
                if (d.Opciones is not null && !d.Opciones.Contains(texto))
                    throw new ValidacionException("valor", $"Valor no válido. Opciones: {string.Join(", ", d.Opciones)}.");
                return texto;
        }
    }
}
