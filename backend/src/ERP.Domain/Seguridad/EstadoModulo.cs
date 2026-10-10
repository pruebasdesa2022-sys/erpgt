namespace ERP.Domain.Seguridad;

/// <summary>Estados de liberación de un módulo. Los nombres coinciden con los valores guardados en la base.</summary>
public enum EstadoModulo
{
    EN_DESARROLLO,
    EN_PRUEBAS,
    NO_LIBERADO,
    LIBERADO,
    SUSPENDIDO,
    RETIRADO,
}

public enum AccesoModulo
{
    /// <summary>Disponible según los permisos del usuario.</summary>
    Disponible,

    /// <summary>Solo para usuarios con el permiso {MODULO}_PRUEBAS.</summary>
    SoloPruebas,

    Bloqueado,
}

public static class ReglasModulo
{
    /// <summary>
    /// Estado efectivo = el más restrictivo entre el global y el de la empresa.
    /// Sin fila para la empresa, el módulo se trata como NO_LIBERADO.
    /// </summary>
    public static EstadoModulo EstadoEfectivo(EstadoModulo global, EstadoModulo? empresa)
    {
        var deEmpresa = empresa ?? EstadoModulo.NO_LIBERADO;
        return Restriccion(global) >= Restriccion(deEmpresa) ? global : deEmpresa;
    }

    public static AccesoModulo Acceso(EstadoModulo efectivo) => efectivo switch
    {
        EstadoModulo.LIBERADO => AccesoModulo.Disponible,
        EstadoModulo.EN_PRUEBAS => AccesoModulo.SoloPruebas,
        _ => AccesoModulo.Bloqueado,
    };

    public static string PermisoPruebas(string codigoModulo) => $"{codigoModulo}_PRUEBAS";

    /// <summary>Un módulo base (Seguridad, Organización, Auditoría) no puede dejar de estar liberado.</summary>
    public static bool CambioPermitido(bool esBase, EstadoModulo nuevo) => !esBase || nuevo == EstadoModulo.LIBERADO;

    // Mayor número = más restrictivo. Los cuatro estados que bloquean pesan igual;
    // entre ellos gana el global, que es el que describe el ambiente.
    private static int Restriccion(EstadoModulo estado) => estado switch
    {
        EstadoModulo.LIBERADO => 0,
        EstadoModulo.EN_PRUEBAS => 1,
        _ => 2,
    };
}
