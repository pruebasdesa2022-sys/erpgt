using Dapper;
using ERP.Application.Comun;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ERP.Infrastructure.Persistencia;

public sealed class OpcionesBaseDatos
{
    public string CadenaConexion { get; set; } = "";
}

/// <summary>
/// Una conexión por solicitud, abierta al primer uso. Todos los repositorios de la solicitud
/// comparten la conexión y, si la hay, la transacción.
/// </summary>
public sealed class UnitOfWork(OpcionesBaseDatos opciones, ILogger<UnitOfWork> logger) : IUnitOfWork, IAsyncDisposable
{
    private const int MaximoIntentos = 3;
    private const int ErrorInterbloqueo = 1213;
    private const int ErrorEsperaDeBloqueo = 1205;

    private MySqlConnection? _conexion;

    static UnitOfWork()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        ManejadoresDapper.Registrar();
    }

    public MySqlTransaction? Transaccion { get; private set; }

    public bool EnTransaccion => Transaccion is not null;

    public async Task<MySqlConnection> ConexionAsync(CancellationToken ct)
    {
        if (_conexion is not null)
            return _conexion;

        // DateTimeKind=Utc: las columnas DATETIME se leen como UTC, que es como se guardan.
        // GuidFormat=None: las columnas CHAR(36) (p. ej. refresh_token.familia_id) se leen como texto, no como Guid.
        var cadena = new MySqlConnectionStringBuilder(opciones.CadenaConexion)
        {
            DateTimeKind = MySqlDateTimeKind.Utc,
            GuidFormat = MySqlGuidFormat.None,
        };
        var conexion = new MySqlConnection(cadena.ConnectionString);
        await conexion.OpenAsync(ct);
        return _conexion = conexion;
    }

    public async Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> accion, CancellationToken ct = default)
    {
        // Un caso de uso que llama a otro dentro de su transacción se une a ella.
        if (EnTransaccion)
            return await accion();

        var conexion = await ConexionAsync(ct);

        for (var intento = 1; ; intento++)
        {
            Transaccion = await conexion.BeginTransactionAsync(ct);
            try
            {
                var resultado = await accion();
                await Transaccion.CommitAsync(ct);
                return resultado;
            }
            catch (MySqlException ex) when (ex.Number is ErrorInterbloqueo or ErrorEsperaDeBloqueo && intento < MaximoIntentos)
            {
                await RevertirAsync();
                logger.LogWarning("Interbloqueo en la base de datos (error {Numero}); reintento {Intento} de {Maximo}",
                    ex.Number, intento, MaximoIntentos - 1);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * intento), ct);
            }
            catch
            {
                await RevertirAsync();
                throw;
            }
            finally
            {
                if (Transaccion is not null)
                    await Transaccion.DisposeAsync();
                Transaccion = null;
            }
        }
    }

    public Task EjecutarEnTransaccionAsync(Func<Task> accion, CancellationToken ct = default) =>
        EjecutarEnTransaccionAsync(async () =>
        {
            await accion();
            return true;
        }, ct);

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
            await _conexion.DisposeAsync();
    }

    private async Task RevertirAsync()
    {
        try
        {
            if (Transaccion is not null)
                await Transaccion.RollbackAsync();
        }
        catch (Exception ex)
        {
            // La conexión pudo haberse perdido; el servidor revierte al cerrarla.
            logger.LogWarning(ex, "No se pudo revertir la transacción");
        }
    }
}

/// <summary>Base de los repositorios: ejecuta Dapper con la conexión y la transacción de la solicitud.</summary>
public abstract class RepositorioBase(UnitOfWork unidadDeTrabajo)
{
    /// <summary>Sufijo para bloquear filas leídas; solo tiene sentido dentro de una transacción.</summary>
    protected bool EnTransaccion => unidadDeTrabajo.EnTransaccion;

    protected string ParaActualizar => unidadDeTrabajo.EnTransaccion ? " FOR UPDATE" : "";

    protected async Task<IReadOnlyList<T>> ConsultarAsync<T>(string sql, object? parametros, CancellationToken ct)
    {
        var conexion = await unidadDeTrabajo.ConexionAsync(ct);
        return (await conexion.QueryAsync<T>(Comando(sql, parametros, ct))).AsList();
    }

    protected async Task<T?> ConsultarUnoAsync<T>(string sql, object? parametros, CancellationToken ct)
    {
        var conexion = await unidadDeTrabajo.ConexionAsync(ct);
        return await conexion.QueryFirstOrDefaultAsync<T>(Comando(sql, parametros, ct));
    }

    protected async Task<T> EscalarAsync<T>(string sql, object? parametros, CancellationToken ct)
    {
        var conexion = await unidadDeTrabajo.ConexionAsync(ct);
        return (await conexion.ExecuteScalarAsync<T>(Comando(sql, parametros, ct)))!;
    }

    protected async Task<int> EjecutarAsync(string sql, object? parametros, CancellationToken ct)
    {
        var conexion = await unidadDeTrabajo.ConexionAsync(ct);
        return await conexion.ExecuteAsync(Comando(sql, parametros, ct));
    }

    /// <summary>Inserta y devuelve el id generado.</summary>
    protected async Task<long> InsertarAsync(string sql, object? parametros, CancellationToken ct) =>
        Convert.ToInt64(await EscalarAsync<object>(sql + "; SELECT LAST_INSERT_ID();", parametros, ct));

    private CommandDefinition Comando(string sql, object? parametros, CancellationToken ct) =>
        new(sql, parametros, unidadDeTrabajo.Transaccion, cancellationToken: ct);
}

/// <summary>Ordenamiento con lista blanca: el cliente nunca aporta nombres de columna.</summary>
public static class Ordenamiento
{
    /// <param name="sort">Campo solicitado; con prefijo "-" es descendente.</param>
    /// <param name="permitidos">Campo de la API → expresión SQL.</param>
    /// <param name="porDefecto">Cláusula usada cuando el campo falta o no está permitido.</param>
    public static string Resolver(string? sort, IReadOnlyDictionary<string, string> permitidos, string porDefecto)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return porDefecto;

        var descendente = sort.StartsWith('-');
        var campo = sort.TrimStart('-', '+').Trim();
        return permitidos.TryGetValue(campo, out var columna)
            ? $"{columna} {(descendente ? "DESC" : "ASC")}"
            : porDefecto;
    }

    /// <summary>Patrón para LIKE con los comodines del usuario escapados.</summary>
    public static string? PatronContiene(string? busqueda) =>
        busqueda is null ? null : $"%{busqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
}
