namespace ERP.Domain.Catalogos;

/// <summary>NIT de Guatemala: cuerpo numérico y dígito verificador (0-9 o K), módulo 11.</summary>
public static class Nit
{
    /// <summary>Mayúsculas y sin espacios ni guiones; nulo si queda vacío.</summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var limpio = valor.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        return limpio.Length == 0 ? null : limpio;
    }

    public static bool EsConsumidorFinal(string? valor) => Normalizar(valor) is "CF";

    public static bool EsValido(string? valor)
    {
        var nit = Normalizar(valor);
        if (nit is null || nit.Length < 2 || nit.Length > 13)
            return false;

        var cuerpo = nit[..^1];
        var verificador = nit[^1];
        if (!cuerpo.All(char.IsAsciiDigit) || !(char.IsAsciiDigit(verificador) || verificador == 'K'))
            return false;

        var suma = 0;
        var factor = cuerpo.Length + 1;
        foreach (var digito in cuerpo)
            suma += (digito - '0') * factor--;

        var resto = (11 - suma % 11) % 11;
        var esperado = resto == 10 ? 'K' : (char)('0' + resto);
        return verificador == esperado;
    }
}

/// <summary>La fecha de negocio es la de America/Guatemala (UTC−6, sin horario de verano).</summary>
public static class FechaNegocio
{
    public static readonly TimeSpan DesfaseUtc = TimeSpan.FromHours(-6);

    public static DateOnly Hoy(DateTime ahoraUtc) => DateOnly.FromDateTime(ahoraUtc + DesfaseUtc);
}

public static class Vigencias
{
    /// <summary>Dos rangos [desde, hasta] (hasta nulo = sin fin) se traslapan si comparten algún día.</summary>
    public static bool SeTraslapan(DateOnly desdeA, DateOnly? hastaA, DateOnly desdeB, DateOnly? hastaB) =>
        (hastaA is null || desdeB <= hastaA) && (hastaB is null || desdeA <= hastaB);

    public static bool Contiene(DateOnly desde, DateOnly? hasta, DateOnly fecha) =>
        fecha >= desde && (hasta is null || fecha <= hasta);
}

/// <summary>1 unidad de origen = <see cref="Factor"/> unidades de destino.</summary>
public sealed record AristaConversion(long OrigenId, long DestinoId, decimal Factor);

/// <summary>
/// Conversiones entre unidades como grafo no dirigido. Una conversión nueva es válida solo si une dos
/// componentes distintos: así no hay ciclos (que darían factores contradictorios) ni caminos duplicados.
/// </summary>
public sealed class GrafoConversiones
{
    private readonly Dictionary<long, List<(long Destino, decimal Factor)>> _adyacencia = [];

    public GrafoConversiones(IEnumerable<AristaConversion> aristas)
    {
        foreach (var arista in aristas)
        {
            Agregar(arista.OrigenId, arista.DestinoId, arista.Factor);
            Agregar(arista.DestinoId, arista.OrigenId, 1m / arista.Factor);
        }
    }

    public bool EstanConectadas(long a, long b) => a == b || Factor(a, b) is not null;

    /// <summary>
    /// Factor para pasar de <paramref name="origen"/> a <paramref name="destino"/> (cantidad en destino por
    /// cada unidad de origen), recorriendo las conversiones; nulo si no hay camino. Quien lo usa redondea
    /// el resultado a los decimales de la unidad (<c>MidpointRounding.AwayFromZero</c>).
    /// </summary>
    public decimal? Factor(long origen, long destino)
    {
        if (origen == destino)
            return 1m;

        var visitados = new HashSet<long> { origen };
        var cola = new Queue<(long Unidad, decimal Acumulado)>();
        cola.Enqueue((origen, 1m));

        while (cola.Count > 0)
        {
            var (unidad, acumulado) = cola.Dequeue();
            if (!_adyacencia.TryGetValue(unidad, out var vecinos))
                continue;

            foreach (var (vecino, factor) in vecinos)
            {
                if (!visitados.Add(vecino))
                    continue;

                var nuevo = acumulado * factor;
                if (vecino == destino)
                    return nuevo;

                cola.Enqueue((vecino, nuevo));
            }
        }

        return null;
    }

    private void Agregar(long desde, long hasta, decimal factor)
    {
        if (!_adyacencia.TryGetValue(desde, out var lista))
            _adyacencia[desde] = lista = [];
        lista.Add((hasta, factor));
    }
}

/// <summary>Número de documento tal como se imprime: [prefijo]-[año]-serie-número con ceros a la izquierda.</summary>
public static class FormatoNumeroDocumento
{
    public static string Formatear(string serie, string? prefijo, bool incluirAnio, int longitud, ulong numero, DateOnly fecha)
    {
        var partes = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(prefijo))
            partes.Add(prefijo.Trim());
        if (incluirAnio)
            partes.Add(fecha.Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture));
        partes.Add(serie);
        partes.Add(numero.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(longitud, '0'));
        return string.Join('-', partes);
    }
}
