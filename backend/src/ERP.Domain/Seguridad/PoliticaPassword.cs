namespace ERP.Domain.Seguridad;

public sealed record PoliticaPassword(
    int LongitudMinima,
    bool RequiereMayuscula,
    bool RequiereMinuscula,
    bool RequiereDigito,
    bool RequiereSimbolo,
    int HistorialCantidad,
    int ExpiraDias,
    int MaxIntentosFallidos,
    int BloqueoMinutos,
    int RecuperacionMinutos)
{
    public const int LongitudMaxima = 128;

    /// <summary>Devuelve los incumplimientos de complejidad; vacío si la contraseña es aceptable.</summary>
    public IReadOnlyList<string> Validar(string? password)
    {
        var errores = new List<string>();
        var valor = password ?? string.Empty;

        if (valor.Length < LongitudMinima)
            errores.Add($"Debe tener al menos {LongitudMinima} caracteres.");
        if (valor.Length > LongitudMaxima)
            errores.Add($"No puede tener más de {LongitudMaxima} caracteres.");
        if (RequiereMayuscula && !valor.Any(char.IsUpper))
            errores.Add("Debe incluir al menos una letra mayúscula.");
        if (RequiereMinuscula && !valor.Any(char.IsLower))
            errores.Add("Debe incluir al menos una letra minúscula.");
        if (RequiereDigito && !valor.Any(char.IsDigit))
            errores.Add("Debe incluir al menos un número.");
        if (RequiereSimbolo && valor.All(char.IsLetterOrDigit))
            errores.Add("Debe incluir al menos un símbolo.");

        return errores;
    }

    /// <summary>La contraseña expiró si la política define vigencia y ya pasó desde el último cambio.</summary>
    public bool Expirada(DateTime? cambiadaEnUtc, DateTime ahoraUtc) =>
        ExpiraDias > 0 && cambiadaEnUtc is { } cambiada && cambiada.AddDays(ExpiraDias) <= ahoraUtc;
}
