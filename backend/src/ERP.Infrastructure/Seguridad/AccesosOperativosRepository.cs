using ERP.Application.Seguridad;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Seguridad;

public sealed class AccesosOperativosRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IAccesosOperativosRepository
{
    public async Task<bool> UsuarioEnEmpresaAsync(long usuarioId, long empresaId, CancellationToken ct) =>
        await EscalarAsync<long>(
            "SELECT COUNT(*) FROM usuario u JOIN usuario_empresa ue ON ue.usuario_id = u.id AND ue.empresa_id = @empresaId " +
            $"WHERE u.id = @usuarioId{(EnTransaccion ? " FOR UPDATE OF u" : "")}",
            new { usuarioId, empresaId }, ct) > 0;

    public async Task<AccesosOperativosDto> ObtenerAsync(long usuarioId, long empresaId, CancellationToken ct) =>
        new(
            await ConsultarAsync<long>(
                """
                SELECT CAST(ub.bodega_id AS SIGNED)
                FROM usuario_bodega ub JOIN bodega b ON b.id = ub.bodega_id AND b.empresa_id = @empresaId
                WHERE ub.usuario_id = @usuarioId ORDER BY ub.es_predeterminada DESC, ub.id
                """, new { usuarioId, empresaId }, ct),
            await ConsultarAsync<long>(
                """
                SELECT CAST(uc.caja_id AS SIGNED)
                FROM usuario_caja uc JOIN caja c ON c.id = uc.caja_id AND c.empresa_id = @empresaId
                WHERE uc.usuario_id = @usuarioId ORDER BY uc.es_predeterminada DESC, uc.id
                """, new { usuarioId, empresaId }, ct));

    public async Task<int> ContarBodegasAsignablesAsync(long usuarioId, long empresaId, IReadOnlyCollection<long> bodegaIds, CancellationToken ct)
    {
        if (bodegaIds.Count == 0)
            return 0;

        return (int)await EscalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM bodega b
            JOIN usuario_sucursal us ON us.sucursal_id = b.sucursal_id AND us.usuario_id = @usuarioId
            WHERE b.id IN @bodegaIds AND b.empresa_id = @empresaId AND b.activo = 1
            """, new { usuarioId, empresaId, bodegaIds }, ct);
    }

    public async Task<int> ContarCajasAsignablesAsync(long usuarioId, long empresaId, IReadOnlyCollection<long> cajaIds, CancellationToken ct)
    {
        if (cajaIds.Count == 0)
            return 0;

        return (int)await EscalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM caja c
            JOIN usuario_sucursal us ON us.sucursal_id = c.sucursal_id AND us.usuario_id = @usuarioId
            WHERE c.id IN @cajaIds AND c.empresa_id = @empresaId AND c.activo = 1
            """, new { usuarioId, empresaId, cajaIds }, ct);
    }

    public async Task ReemplazarAsync(long usuarioId, long empresaId, IReadOnlyList<long> bodegaIds, IReadOnlyList<long> cajaIds, long por, CancellationToken ct)
    {
        // Solo las filas de esta empresa: las bodegas y cajas que el usuario tenga en otras empresas no se tocan.
        await EjecutarAsync(
            "DELETE ub FROM usuario_bodega ub JOIN bodega b ON b.id = ub.bodega_id WHERE ub.usuario_id = @usuarioId AND b.empresa_id = @empresaId",
            new { usuarioId, empresaId }, ct);
        await EjecutarAsync(
            "DELETE uc FROM usuario_caja uc JOIN caja c ON c.id = uc.caja_id WHERE uc.usuario_id = @usuarioId AND c.empresa_id = @empresaId",
            new { usuarioId, empresaId }, ct);

        if (bodegaIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO usuario_bodega (usuario_id, bodega_id, es_predeterminada, creado_por) VALUES (@usuarioId, @bodegaId, @predeterminada, @por)",
                bodegaIds.Select((bodegaId, i) => new { usuarioId, bodegaId, predeterminada = i == 0, por }), ct);

        if (cajaIds.Count > 0)
            await EjecutarAsync(
                "INSERT INTO usuario_caja (usuario_id, caja_id, es_predeterminada, creado_por) VALUES (@usuarioId, @cajaId, @predeterminada, @por)",
                cajaIds.Select((cajaId, i) => new { usuarioId, cajaId, predeterminada = i == 0, por }), ct);
    }

    public async Task<MisAccesosOperativosDto> ListarDeEmpresaAsync(long usuarioId, long empresaId, CancellationToken ct) =>
        new(
            await ConsultarAsync<AccesoBodegaDto>(
                """
                SELECT b.id, b.sucursal_id, b.codigo, b.nombre
                FROM usuario_bodega ub
                JOIN bodega b ON b.id = ub.bodega_id AND b.activo = 1 AND b.empresa_id = @empresaId
                JOIN usuario_sucursal us ON us.sucursal_id = b.sucursal_id AND us.usuario_id = @usuarioId
                WHERE ub.usuario_id = @usuarioId
                ORDER BY ub.es_predeterminada DESC, b.nombre
                """, new { usuarioId, empresaId }, ct),
            await ConsultarAsync<AccesoCajaDto>(
                """
                SELECT c.id, c.sucursal_id, c.bodega_id, c.codigo, c.nombre
                FROM usuario_caja uc
                JOIN caja c ON c.id = uc.caja_id AND c.activo = 1 AND c.empresa_id = @empresaId
                JOIN usuario_sucursal us ON us.sucursal_id = c.sucursal_id AND us.usuario_id = @usuarioId
                WHERE uc.usuario_id = @usuarioId
                ORDER BY uc.es_predeterminada DESC, c.nombre
                """, new { usuarioId, empresaId }, ct));
}
