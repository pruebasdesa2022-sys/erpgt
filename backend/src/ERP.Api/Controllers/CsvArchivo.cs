using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>Exportación CSV de los catálogos: UTF-8 con BOM (Excel respeta las tildes) y celdas a prueba de fórmulas.</summary>
public static class CsvArchivo
{
    public static string SiNo(bool valor) => valor ? "Sí" : "No";

    /// <summary>Número con punto decimal, sin separadores de miles.</summary>
    public static string Numero(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);

    public static FileContentResult Descarga(
        ControllerBase controlador, string nombre, IReadOnlyList<string> encabezados, IEnumerable<IReadOnlyList<string?>> filas)
    {
        var csv = new StringBuilder();
        csv.AppendJoin(',', encabezados.Select(Celda)).Append("\r\n");
        foreach (var fila in filas)
            csv.AppendJoin(',', fila.Select(Celda)).Append("\r\n");

        var contenido = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return controlador.File(contenido, "text/csv; charset=utf-8", nombre);
    }

    /// <summary>
    /// Escapa una celda. Los valores que empiezan con = + - @ se prefijan con comilla simple para que una hoja
    /// de cálculo no los ejecute como fórmula.
    /// </summary>
    internal static string Celda(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return "";

        var texto = valor[0] is '=' or '+' or '-' or '@' ? "'" + valor : valor;
        return texto.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : texto;
    }
}
