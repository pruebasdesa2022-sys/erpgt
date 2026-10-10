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
    /// <summary>El usuario existe y tiene acceso a la empresa; con bloqueo de su fila dentro de una transacción.</summary>
    Task<bool> UsuarioEnEmpresaAsync(long usuarioId, long empresaId, CancellationToken ct);

    /// <summary>Bodegas y cajas del usuario en esa empresa (las de otras empresas no se ven ni se tocan).</summary>
    Task<AccesosOperativosDto> ObtenerAsync(long usuarioId, long empresaId, CancellationToken ct);

    /// <summary>
    /// Cuántas de las bodegas indicadas están activas, son de la empresa y pertenecen a una sucursal a la que el
    /// usuario tiene acceso.
    /// </summary>
    Task<int> ContarBodegasAsignablesAsync(long usuarioId, long empresaId, IReadOnlyCollection<long> bodegaIds, CancellationToken ct);

    Task<int> ContarCajasAsignablesAsync(long usuarioId, long empresaId, IReadOnlyCollection<long> cajaIds, CancellationToken ct);

    /// <summary>Reemplaza solo las bodegas y cajas de la empresa; las de otras empresas quedan intactas.</summary>
    Task ReemplazarAsync(long usuarioId, long empresaId, IReadOnlyList<long> bodegaIds, IReadOnlyList<long> cajaIds, long por, CancellationToken ct);

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
        var empresaId = actual.EmpresaIdRequerida();
        if (!await repositorio.UsuarioEnEmpresaAsync(usuarioId, empresaId, ct))
            throw new NoEncontradoException("El usuario no existe en la empresa activa.");

        return await repositorio.ObtenerAsync(usuarioId, empresaId, ct);
    }

    public Task<AccesosOperativosDto> GuardarAsync(long usuarioId, GuardarAccesosOperativosDto dto, CancellationToken ct)
    {
        var por = actual.UsuarioIdRequerido();
        var empresaId = actual.EmpresaIdRequerida();
        var bodegas = dto.BodegaIds.Distinct().ToList();
        var cajas = dto.CajaIds.Distinct().ToList();

        return unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            if (!await repositorio.UsuarioEnEmpresaAsync(usuarioId, empresaId, ct))
                throw new NoEncontradoException("El usuario no existe en la empresa activa.");

            var errores = new Dictionary<string, string[]>();
            if (await repositorio.ContarBodegasAsignablesAsync(usuarioId, empresaId, bodegas, ct) != bodegas.Count)
                errores["bodegaIds"] = ["Cada bodega debe estar activa, ser de la empresa y pertenecer a una sucursal a la que el usuario tiene acceso."];
            if (await repositorio.ContarCajasAsignablesAsync(usuarioId, empresaId, cajas, ct) != cajas.Count)
                errores["cajaIds"] = ["Cada caja debe estar activa, ser de la empresa y pertenecer a una sucursal a la que el usuario tiene acceso."];
            if (errores.Count > 0)
                throw new ValidacionException(errores);

            var anterior = await repositorio.ObtenerAsync(usuarioId, empresaId, ct);
            await repositorio.ReemplazarAsync(usuarioId, empresaId, bodegas, cajas, por, ct);
            var nuevo = await repositorio.ObtenerAsync(usuarioId, empresaId, ct);

            await auditoria.RegistrarAsync(
                new RegistroAuditoria("SEGURIDAD", "usuario_acceso_operativo", usuarioId.ToString(), "EDITAR", anterior, nuevo), ct);
            return nuevo;
        }, ct);
    }

    /// <summary>Bodegas y cajas del usuario autenticado en su empresa activa.</summary>
    public Task<MisAccesosOperativosDto> MisAccesosAsync(CancellationToken ct) =>
        repositorio.ListarDeEmpresaAsync(actual.UsuarioIdRequerido(), actual.EmpresaIdRequerida(), ct);
}
