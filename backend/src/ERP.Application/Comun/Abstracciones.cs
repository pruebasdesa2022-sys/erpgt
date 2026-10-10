using ERP.Domain.Comun;

namespace ERP.Application.Comun;

/// <summary>Reloj del sistema. Nunca se usa DateTime.Now/UtcNow directamente.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Unidad de trabajo por solicitud: una conexión y, dentro de <c>EjecutarEnTransaccionAsync</c>, una transacción
/// compartida por todos los repositorios. La acción se repite completa ante interbloqueo, así que no debe
/// tener efectos externos (correo, archivos): esos van después de confirmar.
/// </summary>
public interface IUnitOfWork
{
    Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> accion, CancellationToken ct = default);

    Task EjecutarEnTransaccionAsync(Func<Task> accion, CancellationToken ct = default);
}

/// <summary>Datos de la solicitud en curso. Los identificadores son nulos en endpoints anónimos.</summary>
public interface IUsuarioActual
{
    long? UsuarioId { get; }

    long? EmpresaId { get; }

    long? SucursalId { get; }

    string? Ip { get; }

    string? UserAgent { get; }

    /// <summary>Correlation id: el mismo valor aparece en logs, auditoría y cabecera de respuesta.</summary>
    string OperacionId { get; }
}

public static class UsuarioActualExtensions
{
    public static long UsuarioIdRequerido(this IUsuarioActual actual) =>
        actual.UsuarioId ?? throw new NoAutenticadoException("NO_AUTENTICADO", "Debe iniciar sesión.");

    public static long EmpresaIdRequerida(this IUsuarioActual actual) =>
        actual.EmpresaId ?? throw new AccesoDenegadoException("CONTEXTO_INVALIDO", "No hay una empresa activa en la sesión.");
}

public record ConsultaPaginada
{
    public const int PageSizeMaximo = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    /// <summary>Campo de orden; con prefijo "-" es descendente. Solo se aceptan campos de la lista blanca.</summary>
    public string? Sort { get; init; }

    public string? Q { get; init; }

    public int PaginaNormalizada => Math.Max(Page, 1);

    public int TamanoNormalizado => Math.Clamp(PageSize, 1, PageSizeMaximo);

    public int Desplazamiento => (PaginaNormalizada - 1) * TamanoNormalizado;

    public string? Busqueda => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();
}

public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
