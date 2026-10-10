using ERP.Application.Seguridad;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Seguridad;

public sealed class AccesosOperativosRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IAccesosOperativosRepository
{
    public async Task<bool> UsuarioExisteAsync(long usuarioId, CancellationToken ct) =>
        await EscalarAsync<long>($"SELECT COUNT(*) FROM usuario WHERE id = @usuarioId{ParaActualizar}", new { usuarioId }, ct) > 0;

    public async Task<AccesosOperativosDto> ObtenerAsync(long usuarioId, CancellationToken ct) =>
        new(
            await ConsultarAsync<long>(
                "SELECT CAST(bodega_id AS SIGNED) FROM usuario_bodega WHERE usuario_id = @usuarioId ORDER BY es_predeterminada DESC, id",
                new { usuarioId }, ct),
            await ConsultarAsync<long>(
                "SELECT CAST(caja_id AS SIGNED) FROM usuario_caja WHERE usuario_id = @usuarioId ORDER BY es_predeterminada DESC, id",
                new { usuarioId }, ct));

    public async Task<int> ContarBodegasAsignablesAsync(long usuarioId, IReadOnlyCollection<long> bodegaIds, CancellationToken ct)
    {
        if (bodegaIds.Count == 0)
            return 0;

        return (int)await EscalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM bodega b
            JOIN usuario_sucursal us ON us.sucursal_id = b.sucursal_id AND us.usuario_id = @usuarioId
            JOIN usuario_empresa ue ON ue.empresa_id = b.empresa_id AND ue.usuario_id = @usuarioId
            WHERE b.id IN @bodegaIds AND b.activo = 1
            """, new { usuarioId, bodegaIds }, ct);
    }

    public async Task<int> ContarCajasAsignablesAsync(long usuarioId, IReadOnlyCollection<long> cajaIds, CancellationToken ct)
    {
        if (cajaIds.Count == 0)
            return 0;

        return (int)await EscalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM caja c
            JOIN usuario_sucursal us ON us.sucursal_id = c.sucursal_id AND us.usuario_id = @usuarioId
            JOIN usuario_empresa ue ON ue.empresa_id = c.empresa_id AND ue.usuario_id = @usuarioId
            WHERE c.id IN @cajaIds AND c.activo = 1
            """, new { usuarioId, cajaIds }, ct);
    }

    public async Task ReemplazarAsync(long usuarioId, IReadOnlyList<long> bodegaIds, IReadOnlyList<long> cajaIds, long por, CancellationToken ct)
    {
        await EjecutarAsync("DELETE FROM usuario_bodega WHERE usuario_id = @usuarioId", new { usuarioId }, ct);
        await EjecutarAsync("DELETE FROM usuario_caja WHERE usuario_id = @usuarioId", new { usuarioId }, ct);

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
