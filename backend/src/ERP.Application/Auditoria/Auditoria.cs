using ERP.Application.Comun;
using ERP.Application.Seguridad.Contratos;

namespace ERP.Application.Auditoria;

/// <summary>
/// Registro para la bitácora. Los objetos de valores se serializan a JSON; quien llama pasa
/// instantáneas sin datos sensibles (nunca contraseñas ni tokens).
/// </summary>
public sealed record RegistroAuditoria(
    string Modulo,
    string Entidad,
    string? EntidadId,
    string Accion,
    object? ValoresAnteriores = null,
    object? ValoresNuevos = null,
    string? Mensaje = null,
    bool Exito = true,
    long? EmpresaId = null);

public sealed record EventoAcceso(
    string Evento,
    bool Exito,
    long? UsuarioId,
    string? NombreUsuario,
    string? Mensaje = null);

/// <summary>Contrato del módulo Auditoría. Escribe con la transacción en curso si la hay.</summary>
public interface IAuditoria
{
    Task RegistrarAsync(RegistroAuditoria registro, CancellationToken ct = default);

    Task RegistrarAccesoAsync(EventoAcceso evento, CancellationToken ct = default);
}

public static class EventosAcceso
{
    public const string LoginExitoso = "LOGIN_EXITOSO";
    public const string LoginFallido = "LOGIN_FALLIDO";
    public const string Bloqueo = "BLOQUEO";
    public const string Logout = "LOGOUT";
    public const string Refresh = "REFRESH";
    public const string RefreshReutilizado = "REFRESH_REUTILIZADO";
    public const string CambioPassword = "CAMBIO_PASSWORD";
    public const string RecuperacionSolicitada = "RECUPERACION_SOLICITADA";
    public const string RecuperacionUsada = "RECUPERACION_USADA";
    public const string CambioContexto = "CAMBIO_CONTEXTO";
}

public sealed record ConsultaBitacora : ConsultaPaginada
{
    public DateTime? Desde { get; init; }

    public DateTime? Hasta { get; init; }

    public long? UsuarioId { get; init; }

    public string? Entidad { get; init; }

    public string? Accion { get; init; }
}

public sealed record BitacoraDto
{
    public long Id { get; init; }
    public DateTime FechaUtc { get; init; }
    public long? UsuarioId { get; init; }
    public string? NombreUsuario { get; init; }
    public string Modulo { get; init; } = "";
    public string Entidad { get; init; } = "";
    public string? EntidadId { get; init; }
    public string Accion { get; init; } = "";
    public string Resultado { get; init; } = "";
    public string? ValoresAnteriores { get; init; }
    public string? ValoresNuevos { get; init; }
    public string? Ip { get; init; }
    public string? OperacionId { get; init; }
    public string? Mensaje { get; init; }
}

public interface IBitacoraRepository
{
    Task<ResultadoPaginado<BitacoraDto>> ListarAsync(long empresaId, ConsultaBitacora consulta, CancellationToken ct);
}

public sealed class BitacoraService(IBitacoraRepository repositorio, IUsuariosConsulta usuarios, IUsuarioActual actual)
{
    public async Task<ResultadoPaginado<BitacoraDto>> ListarAsync(ConsultaBitacora consulta, CancellationToken ct)
    {
        var pagina = await repositorio.ListarAsync(actual.EmpresaIdRequerida(), consulta, ct);

        // El nombre del usuario es dato de Seguridad: se pide por su contrato, no con un JOIN.
        var ids = pagina.Items.Where(i => i.UsuarioId is not null).Select(i => i.UsuarioId!.Value).Distinct().ToList();
        var nombres = await usuarios.ObtenerNombresAsync(ids, ct);
        var items = pagina.Items
            .Select(i => i.UsuarioId is { } id && nombres.TryGetValue(id, out var nombre) ? i with { NombreUsuario = nombre } : i)
            .ToList();

        return pagina with { Items = items };
    }
}
