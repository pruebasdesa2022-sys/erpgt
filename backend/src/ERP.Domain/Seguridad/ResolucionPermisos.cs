namespace ERP.Domain.Seguridad;

public enum EfectoPermiso
{
    CONCEDER,
    DENEGAR,
}

/// <summary>Permiso asignado directamente a un usuario, con vigencia.</summary>
public sealed record PermisoDirecto(string Codigo, EfectoPermiso Efecto, DateTime VigenteDesdeUtc, DateTime? VigenteHastaUtc)
{
    public bool Vigente(DateTime ahoraUtc) =>
        VigenteDesdeUtc <= ahoraUtc && (VigenteHastaUtc is null || VigenteHastaUtc > ahoraUtc);
}

public static class ResolucionPermisos
{
    /// <summary>
    /// Precedencia: denegación explícita de usuario &gt; concesión explícita de usuario &gt; unión de los roles.
    /// Los permisos directos fuera de vigencia se ignoran.
    /// </summary>
    public static IReadOnlySet<string> Resolver(
        IEnumerable<string> permisosDeRoles,
        IEnumerable<PermisoDirecto> directos,
        DateTime ahoraUtc)
    {
        var efectivos = new HashSet<string>(permisosDeRoles, StringComparer.Ordinal);
        var vigentes = directos.Where(d => d.Vigente(ahoraUtc)).ToList();

        foreach (var concedido in vigentes.Where(d => d.Efecto == EfectoPermiso.CONCEDER))
            efectivos.Add(concedido.Codigo);
        foreach (var denegado in vigentes.Where(d => d.Efecto == EfectoPermiso.DENEGAR))
            efectivos.Remove(denegado.Codigo);

        return efectivos;
    }
}
