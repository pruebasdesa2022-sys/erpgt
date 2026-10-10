using System.Reflection;
using Dapper;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Domain.Comun;
using ERP.Infrastructure.Persistencia;
using MySqlConnector;

namespace ERP.Infrastructure.Catalogos;

/// <summary>Columna propia de un catálogo y la propiedad del DTO que la alimenta.</summary>
public sealed record ColumnaCatalogo(string Columna, string Propiedad);

/// <summary>
/// Declaración de un catálogo: su tabla y sus columnas propias. Todo el SQL del motor se arma con estos
/// textos fijos (nunca con datos de la solicitud); los valores siempre van como parámetros.
/// </summary>
/// <param name="SelectExtra">Columnas propias del DTO, con alias <c>t.</c> y coma inicial.</param>
/// <param name="JoinExtra">JOIN para columnas de descripción (sucursal_nombre, ...).</param>
/// <param name="ColumnasCrear">Columnas propias que se escriben al crear.</param>
/// <param name="ColumnasEditar">Columnas propias que se escriben al editar.</param>
/// <param name="OrdenExtra">Campos de orden adicionales: campo de la API → expresión SQL.</param>
public sealed record DescriptorCatalogo(
    string Tabla,
    CatalogoInfo Info,
    string SelectExtra,
    string JoinExtra,
    IReadOnlyList<ColumnaCatalogo> ColumnasCrear,
    IReadOnlyList<ColumnaCatalogo> ColumnasEditar,
    IReadOnlyDictionary<string, string>? OrdenExtra = null);

public static class Duplicados
{
    private const int ErrorEntradaDuplicada = 1062;

    /// <summary>Traduce la violación de un UNIQUE (carrera entre dos altas) a 409 en lugar de 500.</summary>
    public static async Task<T> TraducirAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (MySqlException ex) when (ex.Number == ErrorEntradaDuplicada)
        {
            throw new ConflictoException("DUPLICADO", "Ya existe un registro con esos datos.");
        }
    }
}

public abstract class CatalogoRepositorio<TDto, TCrear, TEditar>(UnitOfWork unidadDeTrabajo, DescriptorCatalogo descriptor)
    : RepositorioBase(unidadDeTrabajo), ICatalogoRepository<TDto, TCrear, TEditar>
    where TDto : CatalogoDto
    where TCrear : CrearCatalogo
    where TEditar : EditarCatalogo
{
    private static readonly Dictionary<string, string> OrdenBase = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codigo"] = "t.codigo",
        ["nombre"] = "t.nombre",
        ["activo"] = "t.activo",
        ["creadoEn"] = "t.creado_en",
    };

    private readonly string _columnas =
        $"t.id, {(descriptor.Info.Alcance == AlcanceCatalogo.Global ? "NULL" : "t.empresa_id")} AS empresa_id, " +
        $"t.codigo, t.nombre, t.activo, t.version_fila, t.creado_en, t.modificado_en{descriptor.SelectExtra}";

    private readonly string _desde = $"{descriptor.Tabla} t {descriptor.JoinExtra}";

    private readonly string _alcance = descriptor.Info.Alcance switch
    {
        AlcanceCatalogo.Global => "1 = 1",
        AlcanceCatalogo.EmpresaOGlobal => "(t.empresa_id = @empresaId OR t.empresa_id IS NULL)",
        _ => "t.empresa_id = @empresaId",
    };

    private readonly Dictionary<string, string> _orden = descriptor.OrdenExtra is null
        ? OrdenBase
        : new Dictionary<string, string>(OrdenBase, StringComparer.OrdinalIgnoreCase)
            .Concat(descriptor.OrdenExtra).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

    public CatalogoInfo Info => descriptor.Info;

    private string Filtro =>
        $"WHERE {_alcance} AND (@activo IS NULL OR t.activo = @activo) " +
        "AND (@patron IS NULL OR t.codigo LIKE @patron OR t.nombre LIKE @patron)";

    public async Task<ResultadoPaginado<TDto>> ListarAsync(long empresaId, ConsultaCatalogo consulta, CancellationToken ct)
    {
        var parametros = new
        {
            empresaId,
            activo = consulta.Activo,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado,
            desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, _orden, "t.nombre ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM {_desde} {Filtro}", parametros, ct);
        var items = await ConsultarAsync<TDto>(
            $"SELECT {_columnas} FROM {_desde} {Filtro} ORDER BY {orden}, t.id LIMIT @limite OFFSET @desplazamiento", parametros, ct);

        return new ResultadoPaginado<TDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<IReadOnlyList<TDto>> ListarParaExportarAsync(long empresaId, ConsultaCatalogo consulta, int maximo, CancellationToken ct)
    {
        var orden = Ordenamiento.Resolver(consulta.Sort, _orden, "t.nombre ASC");
        return ConsultarAsync<TDto>(
            $"SELECT {_columnas} FROM {_desde} {Filtro} ORDER BY {orden}, t.id LIMIT @maximo",
            new { empresaId, activo = consulta.Activo, patron = Ordenamiento.PatronContiene(consulta.Busqueda), maximo }, ct);
    }

    public Task<TDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<TDto>(
            // FOR UPDATE OF t: bloquea la fila del catálogo, no las que solo se leen para mostrar nombres.
            $"SELECT {_columnas} FROM {_desde} WHERE t.id = @id AND {_alcance}{(EnTransaccion ? " FOR UPDATE OF t" : "")}",
            new { id, empresaId }, ct);

    public async Task<bool> ExisteCodigoAsync(long empresaId, string codigo, CancellationToken ct)
    {
        // En EmpresaOGlobal el código tampoco puede repetir uno estándar.
        var sql = $"SELECT COUNT(*) FROM {descriptor.Tabla} t WHERE {_alcance} AND t.codigo = @codigo";
        return await EscalarAsync<long>(sql, new { empresaId, codigo }, ct) > 0;
    }

    public Task<long> InsertarAsync(long empresaId, string codigo, TCrear dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct)
    {
        var tieneEmpresa = descriptor.Info.Alcance != AlcanceCatalogo.Global;
        var columnas = new List<string>();
        var valores = new List<string>();
        if (tieneEmpresa)
        {
            columnas.Add("empresa_id");
            valores.Add("@empresaId");
        }

        columnas.AddRange(["codigo", "nombre"]);
        valores.AddRange(["@Codigo", "@Nombre"]);
        foreach (var c in descriptor.ColumnasCrear)
        {
            columnas.Add(c.Columna);
            valores.Add("@" + c.Propiedad);
        }

        columnas.AddRange(["creado_en", "creado_por"]);
        valores.AddRange(["@ahoraUtc", "@usuarioId"]);

        var parametros = ParametrosDe(dto);
        parametros.Add("empresaId", empresaId);
        parametros.Add("Codigo", codigo);
        parametros.Add("ahoraUtc", ahoraUtc);
        parametros.Add("usuarioId", usuarioId);

        return Duplicados.TraducirAsync(() => InsertarAsync(
            $"INSERT INTO {descriptor.Tabla} ({string.Join(", ", columnas)}) VALUES ({string.Join(", ", valores)})", parametros, ct));
    }

    public Task<bool> ActualizarAsync(long empresaId, long id, TEditar dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct)
    {
        var asignaciones = new List<string> { "t.nombre = @Nombre" };
        asignaciones.AddRange(descriptor.ColumnasEditar.Select(c => $"t.{c.Columna} = @{c.Propiedad}"));
        asignaciones.AddRange(["t.activo = @Activo", "t.version_fila = t.version_fila + 1", "t.modificado_en = @ahoraUtc", "t.modificado_por = @usuarioId"]);

        var parametros = ParametrosDe(dto);
        parametros.Add("id", id);
        parametros.Add("empresaId", empresaId);
        parametros.Add("ahoraUtc", ahoraUtc);
        parametros.Add("usuarioId", usuarioId);

        return Duplicados.TraducirAsync(async () =>
            await EjecutarAsync(
                $"UPDATE {descriptor.Tabla} t SET {string.Join(", ", asignaciones)} " +
                $"WHERE t.id = @id AND t.version_fila = @VersionFila AND {AlcanceEscritura}", parametros, ct) == 1);
    }

    public async Task<bool> DesactivarAsync(long empresaId, long id, uint versionFila, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            $"UPDATE {descriptor.Tabla} t SET t.activo = 0, t.version_fila = t.version_fila + 1, " +
            "t.modificado_en = @ahoraUtc, t.modificado_por = @usuarioId " +
            $"WHERE t.id = @id AND t.version_fila = @versionFila AND {AlcanceEscritura}",
            new { id, empresaId, versionFila, ahoraUtc, usuarioId }, ct) == 1;

    /// <summary>Las filas estándar (empresa_id nulo) nunca se escriben desde la aplicación.</summary>
    private string AlcanceEscritura => descriptor.Info.Alcance == AlcanceCatalogo.Global ? "1 = 1" : "t.empresa_id = @empresaId";

    /// <summary>Parámetros con los valores del DTO; los textos se recortan y los vacíos pasan a NULL.</summary>
    private static DynamicParameters ParametrosDe(object dto)
    {
        var parametros = new DynamicParameters();
        foreach (var propiedad in dto.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var valor = propiedad.GetValue(dto);
            if (valor is string texto)
                valor = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            parametros.Add(propiedad.Name, valor);
        }

        return parametros;
    }
}

// ----- Declaración de cada catálogo -----

file static class Cols
{
    public static readonly IReadOnlyList<ColumnaCatalogo> Ninguna = [];
}

public sealed class BodegaRepository(UnitOfWork uow) : CatalogoRepositorio<BodegaDto, CrearBodegaDto, EditarBodegaDto>(
    uow,
    new DescriptorCatalogo(
        "bodega", new CatalogoInfo("ORGANIZACION", "bodega", "bodega", true),
        ", t.sucursal_id, s.nombre AS sucursal_nombre, t.descripcion",
        "JOIN sucursal s ON s.id = t.sucursal_id",
        [new("sucursal_id", "SucursalId"), new("descripcion", "Descripcion")],
        [new("descripcion", "Descripcion")],
        new Dictionary<string, string> { ["sucursalNombre"] = "s.nombre" }));

public sealed class CajaRepository(UnitOfWork uow) : CatalogoRepositorio<CajaDto, CrearCajaDto, EditarCajaDto>(
    uow,
    new DescriptorCatalogo(
        "caja", new CatalogoInfo("ORGANIZACION", "caja", "caja", true),
        ", t.sucursal_id, s.nombre AS sucursal_nombre, t.bodega_id, b.nombre AS bodega_nombre",
        "JOIN sucursal s ON s.id = t.sucursal_id LEFT JOIN bodega b ON b.id = t.bodega_id",
        [new("sucursal_id", "SucursalId"), new("bodega_id", "BodegaId")],
        [new("bodega_id", "BodegaId")],
        new Dictionary<string, string> { ["sucursalNombre"] = "s.nombre", ["bodegaNombre"] = "b.nombre" }));

public sealed class DepartamentoRepository(UnitOfWork uow) : CatalogoRepositorio<DepartamentoDto, CrearDepartamentoDto, EditarDepartamentoDto>(
    uow,
    new DescriptorCatalogo("departamento", new CatalogoInfo("ORGANIZACION", "departamento", "departamento", false), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class CentroCostoRepository(UnitOfWork uow) : CatalogoRepositorio<CentroCostoDto, CrearCentroCostoDto, EditarCentroCostoDto>(
    uow,
    new DescriptorCatalogo(
        "centro_costo", new CatalogoInfo("ORGANIZACION", "centro_costo", "centro de costo", false),
        ", t.departamento_id, d.nombre AS departamento_nombre",
        "LEFT JOIN departamento d ON d.id = t.departamento_id",
        [new("departamento_id", "DepartamentoId")],
        [new("departamento_id", "DepartamentoId")],
        new Dictionary<string, string> { ["departamentoNombre"] = "d.nombre" }));

public sealed class TipoProductoRepository(UnitOfWork uow) : CatalogoRepositorio<TipoProductoDto, CrearTipoProductoDto, EditarTipoProductoDto>(
    uow,
    new DescriptorCatalogo(
        "tipo_producto", new CatalogoInfo("CATALOGOS", "tipo_producto", "tipo de producto", false),
        ", t.maneja_inventario", "",
        [new("maneja_inventario", "ManejaInventario")], [new("maneja_inventario", "ManejaInventario")]));

public sealed class CategoriaRepository(UnitOfWork uow) : CatalogoRepositorio<CategoriaDto, CrearCategoriaDto, EditarCategoriaDto>(
    uow, new DescriptorCatalogo("categoria", new CatalogoInfo("CATALOGOS", "categoria", "categoría", true), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class MarcaRepository(UnitOfWork uow) : CatalogoRepositorio<MarcaDto, CrearMarcaDto, EditarMarcaDto>(
    uow, new DescriptorCatalogo("marca", new CatalogoInfo("CATALOGOS", "marca", "marca", true), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class TipoProveedorRepository(UnitOfWork uow) : CatalogoRepositorio<TipoProveedorDto, CrearTipoProveedorDto, EditarTipoProveedorDto>(
    uow, new DescriptorCatalogo("tipo_proveedor", new CatalogoInfo("CATALOGOS", "tipo_proveedor", "tipo de proveedor", false), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class MotivoAnulacionRepository(UnitOfWork uow) : CatalogoRepositorio<MotivoAnulacionDto, CrearMotivoAnulacionDto, EditarMotivoAnulacionDto>(
    uow, new DescriptorCatalogo("motivo_anulacion", new CatalogoInfo("CATALOGOS", "motivo_anulacion", "motivo de anulación", false), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class BancoRepository(UnitOfWork uow) : CatalogoRepositorio<BancoDto, CrearBancoDto, EditarBancoDto>(
    uow, new DescriptorCatalogo("banco", new CatalogoInfo("CATALOGOS", "banco", "banco", false), "", "", Cols.Ninguna, Cols.Ninguna));

public sealed class FormaPagoRepository(UnitOfWork uow) : CatalogoRepositorio<FormaPagoDto, CrearFormaPagoDto, EditarFormaPagoDto>(
    uow,
    new DescriptorCatalogo(
        "forma_pago", new CatalogoInfo("CATALOGOS", "forma_pago", "forma de pago", true),
        ", t.tipo, t.requiere_referencia", "",
        [new("tipo", "Tipo"), new("requiere_referencia", "RequiereReferencia")],
        [new("tipo", "Tipo"), new("requiere_referencia", "RequiereReferencia")]));

public sealed class CondicionPagoRepository(UnitOfWork uow) : CatalogoRepositorio<CondicionPagoDto, CrearCondicionPagoDto, EditarCondicionPagoDto>(
    uow,
    new DescriptorCatalogo(
        "condicion_pago", new CatalogoInfo("CATALOGOS", "condicion_pago", "condición de pago", true),
        ", t.dias_credito", "",
        [new("dias_credito", "DiasCredito")], [new("dias_credito", "DiasCredito")],
        new Dictionary<string, string> { ["diasCredito"] = "t.dias_credito" }));

public sealed class MonedaRepository(UnitOfWork uow) : CatalogoRepositorio<MonedaDto, CrearMonedaDto, EditarMonedaDto>(
    uow,
    new DescriptorCatalogo(
        "moneda", new CatalogoInfo("CATALOGOS", "moneda", "moneda", true, AlcanceCatalogo.Global),
        ", t.simbolo, t.decimales", "",
        [new("simbolo", "Simbolo"), new("decimales", "Decimales")],
        [new("simbolo", "Simbolo"), new("decimales", "Decimales")]));

public sealed class UnidadMedidaRepository(UnitOfWork uow) : CatalogoRepositorio<UnidadMedidaDto, CrearUnidadMedidaDto, EditarUnidadMedidaDto>(
    uow,
    new DescriptorCatalogo(
        "unidad_medida", new CatalogoInfo("CATALOGOS", "unidad_medida", "unidad de medida", true, AlcanceCatalogo.EmpresaOGlobal),
        ", t.magnitud, t.decimales", "",
        [new("magnitud", "Magnitud"), new("decimales", "Decimales")],
        [new("decimales", "Decimales")],
        new Dictionary<string, string> { ["magnitud"] = "t.magnitud" }));

public sealed class CuentaBancariaRepository(UnitOfWork uow) : CatalogoRepositorio<CuentaBancariaDto, CrearCuentaBancariaDto, EditarCuentaBancariaDto>(
    uow,
    new DescriptorCatalogo(
        "cuenta_bancaria", new CatalogoInfo("CATALOGOS", "cuenta_bancaria", "cuenta bancaria", true),
        ", t.banco_id, b.nombre AS banco_nombre, t.moneda_id, m.codigo AS moneda_codigo, t.numero_cuenta, t.tipo_cuenta, t.titular",
        "JOIN banco b ON b.id = t.banco_id JOIN moneda m ON m.id = t.moneda_id",
        [new("banco_id", "BancoId"), new("moneda_id", "MonedaId"), new("numero_cuenta", "NumeroCuenta"),
         new("tipo_cuenta", "TipoCuenta"), new("titular", "Titular")],
        [new("tipo_cuenta", "TipoCuenta"), new("titular", "Titular")],
        new Dictionary<string, string> { ["bancoNombre"] = "b.nombre", ["monedaCodigo"] = "m.codigo", ["numeroCuenta"] = "t.numero_cuenta" }));

/// <summary>
/// Catálogo global y cerrado: la aplicación solo edita nombre y estado. El alta no se expone (la
/// instancia genérica la exige por contrato, pero ningún endpoint la invoca).
/// </summary>
public sealed class TipoDocumentoRepository(UnitOfWork uow) : CatalogoRepositorio<TipoDocumentoDto, CrearTipoDocumentoDto, EditarTipoDocumentoDto>(
    uow,
    new DescriptorCatalogo(
        "tipo_documento", new CatalogoInfo("CATALOGOS", "tipo_documento", "tipo de documento", false, AlcanceCatalogo.Global),
        ", t.modulo", "", Cols.Ninguna, Cols.Ninguna,
        new Dictionary<string, string> { ["modulo"] = "t.modulo" }));
