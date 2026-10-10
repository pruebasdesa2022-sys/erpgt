using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Organizacion;

public sealed class EmpresaRepository(UnitOfWork unidadDeTrabajo)
    : RepositorioBase(unidadDeTrabajo), IEmpresaRepository, IOrganizacionConsulta
{
    public async Task<IReadOnlyList<EmpresaDto>> ListarAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        return await ConsultarAsync<EmpresaDto>(
            """
            SELECT id, codigo, razon_social, nombre_comercial, nit, direccion, telefono, correo, activo
            FROM empresa
            WHERE id IN @ids
            ORDER BY nombre_comercial
            """, new { ids }, ct);
    }

    public Task<IReadOnlyList<EmpresaResumen>> ListarEmpresasAsync(CancellationToken ct) =>
        ConsultarAsync<EmpresaResumen>(
            "SELECT id, codigo, nombre_comercial FROM empresa WHERE activo = 1 ORDER BY nombre_comercial", null, ct);

    public Task<IReadOnlyList<SucursalResumen>> ListarSucursalesAsync(CancellationToken ct) =>
        ConsultarAsync<SucursalResumen>(
            "SELECT id, empresa_id, codigo, nombre FROM sucursal WHERE activo = 1 ORDER BY nombre", null, ct);
}

public sealed class SucursalRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), ISucursalRepository
{
    private const string Columnas =
        "id, empresa_id, codigo, nombre, direccion, telefono, correo, es_principal, activo, version_fila, creado_en, modificado_en";

    private const string Filtro =
        """
        WHERE empresa_id = @empresaId
          AND (@activo IS NULL OR activo = @activo)
          AND (@patron IS NULL OR codigo LIKE @patron OR nombre LIKE @patron)
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codigo"] = "codigo",
        ["nombre"] = "nombre",
        ["activo"] = "activo",
        ["creadoEn"] = "creado_en",
    };

    public async Task<ResultadoPaginado<SucursalDto>> ListarAsync(long empresaId, ConsultaSucursales consulta, CancellationToken ct)
    {
        var parametros = new
        {
            empresaId,
            activo = consulta.Activo,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado,
            desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "nombre ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM sucursal {Filtro}", parametros, ct);
        var items = await ConsultarAsync<SucursalDto>(
            $"SELECT {Columnas} FROM sucursal {Filtro} ORDER BY {orden}, id LIMIT @limite OFFSET @desplazamiento", parametros, ct);

        return new ResultadoPaginado<SucursalDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<IReadOnlyList<SucursalDto>> ListarParaExportarAsync(long empresaId, ConsultaSucursales consulta, int maximo, CancellationToken ct)
    {
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "nombre ASC");
        return ConsultarAsync<SucursalDto>(
            $"SELECT {Columnas} FROM sucursal {Filtro} ORDER BY {orden}, id LIMIT @maximo",
            new { empresaId, activo = consulta.Activo, patron = Ordenamiento.PatronContiene(consulta.Busqueda), maximo }, ct);
    }

    public Task<SucursalDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<SucursalDto>(
            $"SELECT {Columnas} FROM sucursal WHERE id = @id AND empresa_id = @empresaId{ParaActualizar}", new { id, empresaId }, ct);

    public async Task<bool> ExisteCodigoAsync(long empresaId, string codigo, CancellationToken ct) =>
        await EscalarAsync<long>(
            "SELECT COUNT(*) FROM sucursal WHERE empresa_id = @empresaId AND codigo = @codigo", new { empresaId, codigo }, ct) > 0;

    public Task<long> InsertarAsync(long empresaId, CrearSucursalDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        InsertarAsync(
            """
            INSERT INTO sucursal (empresa_id, codigo, nombre, direccion, telefono, correo, creado_en, creado_por)
            VALUES (@empresaId, @Codigo, @Nombre, @Direccion, @Telefono, @Correo, @ahoraUtc, @usuarioId)
            """,
            new { empresaId, dto.Codigo, dto.Nombre, dto.Direccion, dto.Telefono, dto.Correo, ahoraUtc, usuarioId }, ct);

    public async Task<bool> ActualizarAsync(long empresaId, long id, EditarSucursalDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE sucursal
            SET nombre = @Nombre, direccion = @Direccion, telefono = @Telefono, correo = @Correo, activo = @Activo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE id = @id AND empresa_id = @empresaId AND version_fila = @VersionFila
            """,
            new { id, empresaId, dto.Nombre, dto.Direccion, dto.Telefono, dto.Correo, dto.Activo, dto.VersionFila, ahoraUtc, usuarioId }, ct) == 1;
}
