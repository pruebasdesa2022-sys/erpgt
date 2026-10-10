using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Auditoria;

/// <summary>Escribe y consulta la bitácora. Solo inserta: las tablas de auditoría son inmutables.</summary>
public sealed class AuditoriaRepository(UnitOfWork unidadDeTrabajo, IUsuarioActual actual, IClock reloj)
    : RepositorioBase(unidadDeTrabajo), IAuditoria, IBitacoraRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fechaUtc"] = "fecha_utc",
        ["entidad"] = "entidad",
        ["accion"] = "accion",
    };

    public Task RegistrarAsync(RegistroAuditoria registro, CancellationToken ct = default) =>
        EjecutarAsync(
            """
            INSERT INTO auditoria (fecha_utc, usuario_id, empresa_id, sucursal_id, modulo, entidad, entidad_id, accion, resultado,
                                   valores_anteriores, valores_nuevos, ip, user_agent, operacion_id, mensaje)
            VALUES (@fecha, @usuarioId, @empresaId, @sucursalId, @Modulo, @Entidad, @EntidadId, @Accion, @resultado,
                    @anteriores, @nuevos, @ip, @userAgent, @operacionId, @mensaje)
            """,
            new
            {
                fecha = reloj.UtcNow,
                usuarioId = actual.UsuarioId,
                empresaId = registro.EmpresaId ?? actual.EmpresaId,
                sucursalId = actual.SucursalId,
                registro.Modulo,
                registro.Entidad,
                registro.EntidadId,
                registro.Accion,
                resultado = registro.Exito ? "EXITO" : "FALLO",
                anteriores = Serializar(registro.ValoresAnteriores),
                nuevos = Serializar(registro.ValoresNuevos),
                ip = Recortar(actual.Ip, 45),
                userAgent = Recortar(actual.UserAgent, 300),
                operacionId = Recortar(actual.OperacionId, 64),
                mensaje = Recortar(registro.Mensaje, 1000),
            }, ct);

    public Task RegistrarAccesoAsync(EventoAcceso evento, CancellationToken ct = default) =>
        EjecutarAsync(
            """
            INSERT INTO auditoria_acceso (fecha_utc, usuario_id, nombre_usuario, evento, resultado, ip, user_agent, operacion_id, mensaje)
            VALUES (@fecha, @UsuarioId, @nombreUsuario, @Evento, @resultado, @ip, @userAgent, @operacionId, @mensaje)
            """,
            new
            {
                fecha = reloj.UtcNow,
                evento.UsuarioId,
                nombreUsuario = Recortar(evento.NombreUsuario, 150),
                evento.Evento,
                resultado = evento.Exito ? "EXITO" : "FALLO",
                ip = Recortar(actual.Ip, 45),
                userAgent = Recortar(actual.UserAgent, 300),
                operacionId = Recortar(actual.OperacionId, 64),
                mensaje = Recortar(evento.Mensaje, 500),
            }, ct);

    public async Task<ResultadoPaginado<BitacoraDto>> ListarAsync(long empresaId, ConsultaBitacora consulta, CancellationToken ct)
    {
        const string filtro =
            """
            WHERE empresa_id = @empresaId
              AND (@desde IS NULL OR fecha_utc >= @desde)
              AND (@hasta IS NULL OR fecha_utc < @hasta)
              AND (@usuarioId IS NULL OR usuario_id = @usuarioId)
              AND (@entidad IS NULL OR entidad = @entidad)
              AND (@accion IS NULL OR accion = @accion)
              AND (@patron IS NULL OR entidad LIKE @patron OR entidad_id LIKE @patron OR mensaje LIKE @patron OR operacion_id LIKE @patron)
            """;

        var parametros = new
        {
            empresaId,
            desde = consulta.Desde,
            hasta = consulta.Hasta,
            usuarioId = consulta.UsuarioId,
            entidad = Vacio(consulta.Entidad),
            accion = Vacio(consulta.Accion),
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado,
            desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "fecha_utc DESC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM auditoria {filtro}", parametros, ct);
        var items = await ConsultarAsync<BitacoraDto>(
            $"""
            SELECT id, fecha_utc, usuario_id, modulo, entidad, entidad_id, accion, resultado,
                   CAST(valores_anteriores AS CHAR) AS valores_anteriores, CAST(valores_nuevos AS CHAR) AS valores_nuevos,
                   ip, operacion_id, mensaje
            FROM auditoria {filtro}
            ORDER BY {orden}, id DESC
            LIMIT @limite OFFSET @desplazamiento
            """, parametros, ct);

        return new ResultadoPaginado<BitacoraDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    private static string? Serializar(object? valor) => valor is null ? null : JsonSerializer.Serialize(valor, valor.GetType(), Json);

    private static string? Recortar(string? valor, int maximo) =>
        valor is null || valor.Length <= maximo ? valor : valor[..maximo];

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
