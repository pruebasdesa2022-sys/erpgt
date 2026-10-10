namespace ERP.Application.Seguridad.Contratos;

/// <summary>Lecturas de Seguridad que otros módulos pueden usar.</summary>
public interface IUsuariosConsulta
{
    /// <summary>Nombre de usuario por identificador; los que no existan se omiten.</summary>
    Task<IReadOnlyDictionary<long, string>> ObtenerNombresAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
}
