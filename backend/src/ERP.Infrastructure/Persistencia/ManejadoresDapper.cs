using System.Data;
using Dapper;

namespace ERP.Infrastructure.Persistencia;

/// <summary>Las columnas DATE (fecha de negocio) se leen y escriben como <see cref="DateOnly"/>.</summary>
public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => value switch
    {
        DateOnly fecha => fecha,
        DateTime fechaHora => DateOnly.FromDateTime(fechaHora),
        _ => DateOnly.Parse(value.ToString()!, System.Globalization.CultureInfo.InvariantCulture),
    };

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}

public static class ManejadoresDapper
{
    private static int _registrado;

    /// <summary>Idempotente: se invoca desde el constructor estático de la unidad de trabajo.</summary>
    public static void Registrar()
    {
        if (Interlocked.Exchange(ref _registrado, 1) == 1)
            return;

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }
}
