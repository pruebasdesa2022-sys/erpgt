namespace ERP.Domain.Comun;

/// <summary>Base de los errores esperados del negocio. La API los traduce a ProblemDetails.</summary>
public abstract class ErpException(string errorCode, string mensaje) : Exception(mensaje)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>401: credenciales o sesión inválidas.</summary>
public sealed class NoAutenticadoException(string errorCode, string mensaje) : ErpException(errorCode, mensaje);

/// <summary>403: autenticado pero sin acceso (contexto, módulo o permiso).</summary>
public sealed class AccesoDenegadoException(string errorCode, string mensaje) : ErpException(errorCode, mensaje);

/// <summary>404: el recurso no existe o no pertenece a la empresa activa.</summary>
public sealed class NoEncontradoException(string mensaje) : ErpException("NO_ENCONTRADO", mensaje);

/// <summary>409: duplicado o edición concurrente (version_fila).</summary>
public sealed class ConflictoException(string errorCode, string mensaje) : ErpException(errorCode, mensaje)
{
    public static ConflictoException Concurrencia() =>
        new("CONCURRENCIA", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
}

/// <summary>422: la solicitud es válida pero viola una regla de negocio.</summary>
public sealed class ReglaNegocioException(string errorCode, string mensaje) : ErpException(errorCode, mensaje);

/// <summary>400: errores de validación por campo detectados fuera de FluentValidation.</summary>
public sealed class ValidacionException(IReadOnlyDictionary<string, string[]> errores)
    : ErpException("VALIDACION", "La solicitud contiene datos inválidos.")
{
    public IReadOnlyDictionary<string, string[]> Errores { get; } = errores;

    public ValidacionException(string campo, params string[] mensajes)
        : this(new Dictionary<string, string[]> { [campo] = mensajes })
    {
    }
}
