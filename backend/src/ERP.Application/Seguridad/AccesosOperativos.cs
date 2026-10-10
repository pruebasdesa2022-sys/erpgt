using ERP.Application.Auditoria;
using ERP.Application.Comun;
using ERP.Domain.Comun;

namespace ERP.Application.Seguridad;

// Bodegas y cajas a las que un usuario puede acceder (B3). En la etapa 1A solo se asignan y se
// consultan; el contexto de trabajo (bodega/caja activa) y su validación en cada solicitud llegan con
// los módulos que las usan (Inventario 1C, Caja 1F).

public sealed record AccesosOperativosDto(IReadOnlyList<long> BodegaIds, IReadOnlyList<long> CajaIds);

public sealed record GuardarAccesosOperativosDto(IReadOnlyList<long> BodegaIds, IReadOnlyList<long> CajaIds);

public sealed record AccesoBodegaDto
{
    public long Id { get; init; }
    public long SucursalId { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public sealed record AccesoCajaDto
{
    public long Id { get; init; }
    public long SucursalId { get; init; }
    public long? BodegaId { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
}

public sealed record MisAccesosOperativosDto(IReadOnlyList<AccesoBodegaDto> Bodegas, IReadOnlyList<AccesoCajaDto> Cajas);

public interface IAccesosOperativosRepository
{
    Task<bool> UsuarioExisteAsync(long usuarioId, CancellationToken ct);

    Task<AccesosOperativosDto> ObtenerAsync(long usuarioId, CancellationToken ct);

    /// <summary>
    /// Cuántas de las bodegas indicadas están activas y pertenecen a una sucursal a la que el usuario tiene acceso.
    /// </summary>
    Task<int> ContarBodegasAsignablesAsync(long usuarioId, IReadOnlyCollection<long> bodegaIds, CancellationToken ct);

    Task<int> ContarCajasAsignablesAsync(long usuarioId, IReadOnlyCollection<long> cajaIds, CancellationToken ct);

    Task ReemplazarAsync(long usuarioId, IReadOnlyList<long> bodegaIds, IReadOnlyList<long> cajaIds, long por, CancellationToken ct);

    Task<MisAccesosOperativosDto> ListarDeEmpresaAsync(long usuarioId, long empresaId, CancellationToken ct);
}

public sealed class AccesosOperativosService(
    IAccesosOperativosRepository repositorio,
    IUnitOfWork unidadDeTrabajo,
    IAuditoria auditoria,
    IUsuarioActual actual)
{
    public async Task<AccesosOperativosDto> ObtenerAsync(long usuarioId, CancellationToken ct)
    {
        if (!await repositorio.UsuarioExisteAsync(usuarioId, ct))
            throw new NoEncontradoException("El usuario no existe.");

        return await repositorio.ObtenerAsync(usuarioId, ct);
    }

    public Task<AccesosOperativosDto> GuardarAsync(long usuarioId, GuardarAccesosOperativosDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();
        var bodegas = dto.BodegaIds.Distinct().ToList();
        var cajas = dto.CajaIds.Distinct().ToList();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (!await repositorio.UsuarioExisteAsync(usuarioId, ct))
                throw new NoEncontradoException("El usuario no existe.");

            var errores = new Dictionary<string, string[]>();
            if (await repositorio.ContarBodegasAsignablesAsync(usuarioId, bodegas, ct) != bodegas.Count)
                errores["bodegaIds"] = ["Cada bodega debe estar activa y pertenecer a una sucursal a la que el usuario tiene acceso."];
            if (await repositorio.ContarCajasAsignablesAsync(usuarioId, cajas, ct) != cajas.Count)
                errores["cajaIds"] = ["Cada caja debe estar activa y pertenecer a una sucursal a la que el usuario tiene acceso."];
            if (errores.Count > 0)
                throw new ValidacionException(errores);

            var anterior = await repositorio.ObtenerAsync(usuarioId, ct);
            await repositorio.ReemplazarAsync(usuarioId, bodegas, cajas, por, ct);
            var nuevo = await repositorio.ObtenerAsync(usuarioId, ct);

            await auditoria.RegistrarAsync(
                new RegistroAuditoria("SEGURIDAD", "usuario_acceso_operativo", usuarioId.ToString(), "EDITAR", anterior, nuevo), ct);
            return nuevo;
        }, ct);
    }

    /// <summary>Bodegas y cajas del usuario autenticado en su empresa activa.</summary>
    public Task<MisAccesosOperativosDto> MisAccesosAsync(CancellationToken ct) =>
        repositorio.ListarDeEmpresaAsync(actual.UsuarioIdRequerido(), actual.EmpresaIdRequerida(), ct);
}
