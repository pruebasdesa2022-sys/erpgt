using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Infrastructure.Persistencia;

namespace ERP.Infrastructure.Catalogos;

// ===== Tipo de cambio =====

public sealed class TipoCambioRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), ITipoCambioRepository
{
    private const string Columnas =
        "tc.id, tc.moneda_id, m.codigo AS moneda_codigo, tc.fecha, tc.tasa, tc.activo, tc.version_fila, tc.creado_en, tc.modificado_en";

    private const string Desde = "tipo_cambio tc JOIN moneda m ON m.id = tc.moneda_id";

    private const string Filtro =
        """
        WHERE tc.empresa_id = @empresaId
          AND (@monedaId IS NULL OR tc.moneda_id = @monedaId)
          AND (@desde IS NULL OR tc.fecha >= @desde)
          AND (@hasta IS NULL OR tc.fecha <= @hasta)
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fecha"] = "tc.fecha",
        ["monedaCodigo"] = "m.codigo",
        ["tasa"] = "tc.tasa",
    };

    public async Task<ResultadoPaginado<TipoCambioDto>> ListarAsync(long empresaId, ConsultaTiposCambio consulta, CancellationToken ct)
    {
        var parametros = new
        {
            empresaId, monedaId = consulta.MonedaId, desde = consulta.Desde, hasta = consulta.Hasta,
            limite = consulta.TamanoNormalizado, desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "tc.fecha DESC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM {Desde} {Filtro}", parametros, ct);
        var items = await ConsultarAsync<TipoCambioDto>(
            $"SELECT {Columnas} FROM {Desde} {Filtro} ORDER BY {orden}, tc.id LIMIT @limite OFFSET @desplazamiento", parametros, ct);
        return new ResultadoPaginado<TipoCambioDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<IReadOnlyList<TipoCambioDto>> ListarParaExportarAsync(long empresaId, ConsultaTiposCambio consulta, int maximo, CancellationToken ct) =>
        ConsultarAsync<TipoCambioDto>(
            $"SELECT {Columnas} FROM {Desde} {Filtro} ORDER BY {Ordenamiento.Resolver(consulta.Sort, Orden, "tc.fecha DESC")}, tc.id LIMIT @maximo",
            new { empresaId, monedaId = consulta.MonedaId, desde = consulta.Desde, hasta = consulta.Hasta, maximo }, ct);

    public Task<TipoCambioDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<TipoCambioDto>(
            $"SELECT {Columnas} FROM {Desde} WHERE tc.id = @id AND tc.empresa_id = @empresaId{(EnTransaccion ? " FOR UPDATE OF tc" : "")}",
            new { id, empresaId }, ct);

    public async Task<long?> MonedaLocalIdAsync(long empresaId, CancellationToken ct) =>
        (await ConsultarUnoAsync<long?>("SELECT moneda_local_id FROM empresa WHERE id = @empresaId", new { empresaId }, ct));

    public Task<long> InsertarAsync(long empresaId, CrearTipoCambioDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        Duplicados.TraducirAsync(() => InsertarAsync(
            """
            INSERT INTO tipo_cambio (empresa_id, moneda_id, fecha, tasa, creado_en, creado_por)
            VALUES (@empresaId, @MonedaId, @Fecha, @Tasa, @ahoraUtc, @usuarioId)
            """,
            new { empresaId, dto.MonedaId, dto.Fecha, dto.Tasa, ahoraUtc, usuarioId }, ct));

    public async Task<bool> ActualizarAsync(long empresaId, long id, EditarTipoCambioDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE tipo_cambio
            SET tasa = @Tasa, version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE id = @id AND empresa_id = @empresaId AND version_fila = @VersionFila
            """,
            new { id, empresaId, dto.Tasa, dto.VersionFila, ahoraUtc, usuarioId }, ct) == 1;

    public async Task<TasaVigente?> ObtenerVigenteAsync(long empresaId, long monedaId, DateOnly fecha, CancellationToken ct)
    {
        var local = await MonedaLocalIdAsync(empresaId, ct);
        if (monedaId == local)
            return new TasaVigente { MonedaId = monedaId, FechaTasa = fecha, Tasa = 1m };

        // Índice uq_tipo_cambio (empresa_id, moneda_id, fecha): la última tasa hasta la fecha.
        return await ConsultarUnoAsync<TasaVigente>(
            """
            SELECT moneda_id, fecha AS fecha_tasa, tasa
            FROM tipo_cambio
            WHERE empresa_id = @empresaId AND moneda_id = @monedaId AND fecha <= @fecha AND activo = 1
            ORDER BY fecha DESC
            LIMIT 1
            """, new { empresaId, monedaId, fecha }, ct);
    }
}

// ===== Impuestos =====

public sealed class ImpuestoRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IImpuestoRepository
{
    private const string Columnas =
        "id, codigo, nombre, tipo, porcentaje, vigente_desde, vigente_hasta, activo, version_fila, creado_en, modificado_en";

    private const string Filtro =
        """
        WHERE empresa_id = @empresaId
          AND (@activo IS NULL OR activo = @activo)
          AND (@vigentesA IS NULL OR (activo = 1 AND vigente_desde <= @vigentesA AND (vigente_hasta IS NULL OR vigente_hasta >= @vigentesA)))
          AND (@patron IS NULL OR codigo LIKE @patron OR nombre LIKE @patron)
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codigo"] = "codigo",
        ["nombre"] = "nombre",
        ["porcentaje"] = "porcentaje",
        ["vigenteDesde"] = "vigente_desde",
        ["activo"] = "activo",
    };

    public async Task<ResultadoPaginado<ImpuestoDto>> ListarAsync(long empresaId, ConsultaImpuestos consulta, CancellationToken ct)
    {
        var parametros = new
        {
            empresaId, activo = consulta.Activo, vigentesA = consulta.VigentesA,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado, desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "codigo ASC, vigente_desde DESC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM impuesto {Filtro}", parametros, ct);
        var items = await ConsultarAsync<ImpuestoDto>(
            $"SELECT {Columnas} FROM impuesto {Filtro} ORDER BY {orden}, id LIMIT @limite OFFSET @desplazamiento", parametros, ct);
        return new ResultadoPaginado<ImpuestoDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<IReadOnlyList<ImpuestoDto>> ListarParaExportarAsync(long empresaId, ConsultaImpuestos consulta, int maximo, CancellationToken ct) =>
        ConsultarAsync<ImpuestoDto>(
            $"SELECT {Columnas} FROM impuesto {Filtro} ORDER BY {Ordenamiento.Resolver(consulta.Sort, Orden, "codigo ASC, vigente_desde DESC")}, id LIMIT @maximo",
            new
            {
                empresaId, activo = consulta.Activo, vigentesA = consulta.VigentesA,
                patron = Ordenamiento.PatronContiene(consulta.Busqueda), maximo,
            }, ct);

    public Task<ImpuestoDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<ImpuestoDto>(
            $"SELECT {Columnas} FROM impuesto WHERE id = @id AND empresa_id = @empresaId{ParaActualizar}", new { id, empresaId }, ct);

    public Task<IReadOnlyList<VigenciaImpuesto>> VigenciasActivasAsync(long empresaId, string codigo, CancellationToken ct) =>
        // Con bloqueo: dos altas simultáneas del mismo código no pueden pasar ambas la verificación de traslape.
        ConsultarAsync<VigenciaImpuesto>(
            $"SELECT id, vigente_desde, vigente_hasta FROM impuesto WHERE empresa_id = @empresaId AND codigo = @codigo AND activo = 1{ParaActualizar}",
            new { empresaId, codigo }, ct);

    public Task<long> InsertarAsync(long empresaId, CrearImpuestoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        Duplicados.TraducirAsync(() => InsertarAsync(
            """
            INSERT INTO impuesto (empresa_id, codigo, nombre, tipo, porcentaje, vigente_desde, vigente_hasta, creado_en, creado_por)
            VALUES (@empresaId, @Codigo, @Nombre, @Tipo, @Porcentaje, @VigenteDesde, @VigenteHasta, @ahoraUtc, @usuarioId)
            """,
            new { empresaId, dto.Codigo, dto.Nombre, dto.Tipo, dto.Porcentaje, dto.VigenteDesde, dto.VigenteHasta, ahoraUtc, usuarioId }, ct));

    public async Task<bool> ActualizarAsync(long empresaId, long id, EditarImpuestoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE impuesto
            SET nombre = @Nombre, vigente_hasta = @VigenteHasta, activo = @Activo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE id = @id AND empresa_id = @empresaId AND version_fila = @VersionFila
            """,
            new { id, empresaId, dto.Nombre, dto.VigenteHasta, dto.Activo, dto.VersionFila, ahoraUtc, usuarioId }, ct) == 1;
}

// ===== Conversiones de unidades =====

public sealed class ConversionUnidadRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IConversionUnidadRepository
{
    private const string Columnas =
        """
        c.id, c.empresa_id, c.unidad_origen_id, o.codigo AS unidad_origen_codigo,
        c.unidad_destino_id, d.codigo AS unidad_destino_codigo, o.magnitud, c.factor,
        c.activo, c.version_fila, c.creado_en, c.modificado_en
        """;

    private const string Desde =
        "unidad_conversion c JOIN unidad_medida o ON o.id = c.unidad_origen_id JOIN unidad_medida d ON d.id = c.unidad_destino_id";

    private const string Visible = "(c.empresa_id = @empresaId OR c.empresa_id IS NULL)";

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unidadOrigenCodigo"] = "o.codigo",
        ["unidadDestinoCodigo"] = "d.codigo",
        ["magnitud"] = "o.magnitud",
        ["factor"] = "c.factor",
    };

    public async Task<ResultadoPaginado<ConversionUnidadDto>> ListarAsync(long empresaId, ConsultaConversiones consulta, CancellationToken ct)
    {
        const string filtro =
            "WHERE (c.empresa_id = @empresaId OR c.empresa_id IS NULL) AND (@activo IS NULL OR c.activo = @activo) " +
            "AND (@patron IS NULL OR o.codigo LIKE @patron OR d.codigo LIKE @patron OR o.nombre LIKE @patron OR d.nombre LIKE @patron)";
        var parametros = new
        {
            empresaId, activo = consulta.Activo, patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado, desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "o.magnitud ASC, o.codigo ASC, d.codigo ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM {Desde} {filtro}", parametros, ct);
        var items = await ConsultarAsync<ConversionUnidadDto>(
            $"SELECT {Columnas} FROM {Desde} {filtro} ORDER BY {orden}, c.id LIMIT @limite OFFSET @desplazamiento", parametros, ct);
        return new ResultadoPaginado<ConversionUnidadDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<ConversionUnidadDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<ConversionUnidadDto>(
            $"SELECT {Columnas} FROM {Desde} WHERE c.id = @id AND {Visible}{(EnTransaccion ? " FOR UPDATE OF c" : "")}",
            new { id, empresaId }, ct);

    public Task<IReadOnlyList<ConversionUnidadDto>> ListarActivasAsync(long empresaId, CancellationToken ct) =>
        // Con bloqueo: dos altas simultáneas no pueden cerrar un ciclo cada una sin ver la otra.
        ConsultarAsync<ConversionUnidadDto>(
            $"SELECT {Columnas} FROM {Desde} WHERE {Visible} AND c.activo = 1{(EnTransaccion ? " FOR SHARE OF c" : "")}", new { empresaId }, ct);

    public Task<UnidadBasica?> ObtenerUnidadActivaAsync(long empresaId, long unidadId, CancellationToken ct) =>
        ConsultarUnoAsync<UnidadBasica>(
            "SELECT id, codigo, magnitud FROM unidad_medida WHERE id = @unidadId AND activo = 1 AND (empresa_id = @empresaId OR empresa_id IS NULL)",
            new { unidadId, empresaId }, ct);

    public Task<long> InsertarAsync(long empresaId, CrearConversionUnidadDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        Duplicados.TraducirAsync(() => InsertarAsync(
            """
            INSERT INTO unidad_conversion (empresa_id, unidad_origen_id, unidad_destino_id, factor, creado_en, creado_por)
            VALUES (@empresaId, @UnidadOrigenId, @UnidadDestinoId, @Factor, @ahoraUtc, @usuarioId)
            """,
            new { empresaId, dto.UnidadOrigenId, dto.UnidadDestinoId, dto.Factor, ahoraUtc, usuarioId }, ct));

    public async Task<bool> ActualizarAsync(long empresaId, long id, EditarConversionUnidadDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE unidad_conversion
            SET factor = @Factor, activo = @Activo, version_fila = version_fila + 1,
                modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE id = @id AND empresa_id = @empresaId AND version_fila = @VersionFila
            """,
            new { id, empresaId, dto.Factor, dto.Activo, dto.VersionFila, ahoraUtc, usuarioId }, ct) == 1;
}

// ===== Series y numeración =====

public sealed class SerieDocumentoRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), ISerieDocumentoRepository
{
    private const string Columnas =
        """
        s.id, s.tipo_documento_id, td.codigo AS tipo_documento_codigo, td.nombre AS tipo_documento_nombre,
        s.sucursal_id, su.nombre AS sucursal_nombre, s.caja_id, ca.nombre AS caja_nombre,
        s.serie, s.prefijo, s.incluir_anio, s.longitud, s.siguiente_correlativo, s.numero_maximo,
        s.vigente_desde, s.vigente_hasta, s.activo, s.version_fila, s.creado_en, s.modificado_en
        """;

    private const string Desde =
        """
        serie_documento s
        JOIN tipo_documento td ON td.id = s.tipo_documento_id
        LEFT JOIN sucursal su ON su.id = s.sucursal_id
        LEFT JOIN caja ca ON ca.id = s.caja_id
        """;

    private static readonly Dictionary<string, string> Orden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tipoDocumentoNombre"] = "td.nombre",
        ["serie"] = "s.serie",
        ["siguienteCorrelativo"] = "s.siguiente_correlativo",
        ["vigenteDesde"] = "s.vigente_desde",
        ["activo"] = "s.activo",
    };

    public async Task<ResultadoPaginado<SerieDocumentoDto>> ListarAsync(long empresaId, ConsultaSeries consulta, CancellationToken ct)
    {
        const string filtro =
            """
            WHERE s.empresa_id = @empresaId
              AND (@activo IS NULL OR s.activo = @activo)
              AND (@tipoDocumentoId IS NULL OR s.tipo_documento_id = @tipoDocumentoId)
              AND (@patron IS NULL OR s.serie LIKE @patron OR td.nombre LIKE @patron OR s.prefijo LIKE @patron)
            """;
        var parametros = new
        {
            empresaId, activo = consulta.Activo, tipoDocumentoId = consulta.TipoDocumentoId,
            patron = Ordenamiento.PatronContiene(consulta.Busqueda),
            limite = consulta.TamanoNormalizado, desplazamiento = consulta.Desplazamiento,
        };
        var orden = Ordenamiento.Resolver(consulta.Sort, Orden, "td.nombre ASC, s.serie ASC");

        var total = await EscalarAsync<long>($"SELECT COUNT(*) FROM {Desde} {filtro}", parametros, ct);
        var items = await ConsultarAsync<SerieDocumentoDto>(
            $"SELECT {Columnas} FROM {Desde} {filtro} ORDER BY {orden}, s.id LIMIT @limite OFFSET @desplazamiento", parametros, ct);
        return new ResultadoPaginado<SerieDocumentoDto>(items, consulta.PaginaNormalizada, consulta.TamanoNormalizado, total);
    }

    public Task<SerieDocumentoDto?> ObtenerAsync(long empresaId, long id, CancellationToken ct) =>
        ConsultarUnoAsync<SerieDocumentoDto>(
            $"SELECT {Columnas} FROM {Desde} WHERE s.id = @id AND s.empresa_id = @empresaId{(EnTransaccion ? " FOR UPDATE OF s" : "")}",
            new { id, empresaId }, ct);

    public Task<long> InsertarAsync(long empresaId, CrearSerieDocumentoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        Duplicados.TraducirAsync(() => InsertarAsync(
            """
            INSERT INTO serie_documento
              (empresa_id, sucursal_id, caja_id, tipo_documento_id, serie, prefijo, incluir_anio, longitud,
               siguiente_correlativo, numero_maximo, vigente_desde, vigente_hasta, creado_en, creado_por)
            VALUES
              (@empresaId, @SucursalId, @CajaId, @TipoDocumentoId, @Serie, @Prefijo, @IncluirAnio, @Longitud,
               @SiguienteCorrelativo, @NumeroMaximo, @VigenteDesde, @VigenteHasta, @ahoraUtc, @usuarioId)
            """,
            new
            {
                empresaId, dto.SucursalId, dto.CajaId, dto.TipoDocumentoId, dto.Serie, dto.Prefijo, dto.IncluirAnio, dto.Longitud,
                dto.SiguienteCorrelativo, dto.NumeroMaximo, dto.VigenteDesde, dto.VigenteHasta, ahoraUtc, usuarioId,
            }, ct));

    /// <summary>No toca <c>siguiente_correlativo</c>: solo lo avanza el procedimiento almacenado.</summary>
    public async Task<bool> ActualizarAsync(long empresaId, long id, EditarSerieDocumentoDto dto, long usuarioId, DateTime ahoraUtc, CancellationToken ct) =>
        await EjecutarAsync(
            """
            UPDATE serie_documento
            SET numero_maximo = @NumeroMaximo, vigente_hasta = @VigenteHasta, activo = @Activo,
                version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE id = @id AND empresa_id = @empresaId AND version_fila = @VersionFila
            """,
            new { id, empresaId, dto.NumeroMaximo, dto.VigenteHasta, dto.Activo, dto.VersionFila, ahoraUtc, usuarioId }, ct) == 1;
}

public sealed class NumeracionRepository(UnitOfWork uow) : RepositorioBase(uow), INumeracionRepository
{
    private readonly UnitOfWork _unidadDeTrabajo = uow;

    public async Task<AsignacionSerie?> AvanzarAsync(long empresaId, long serieId, CancellationToken ct)
    {
        var conexion = await _unidadDeTrabajo.ConexionAsync(ct);
        var filas = await Dapper.SqlMapper.QueryAsync<AsignacionSerie>(conexion,
            new Dapper.CommandDefinition("sp_siguiente_correlativo",
                new { p_empresa_id = empresaId, p_serie_documento_id = serieId },
                _unidadDeTrabajo.Transaccion, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
        return filas.FirstOrDefault();
    }
}

// ===== Configuración =====

public sealed class ConfiguracionRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IConfiguracionRepository
{
    public Task<IReadOnlyList<ValorConfiguracion>> VigentesAsync(long empresaId, DateOnly fecha, CancellationToken ct) =>
        ConsultarAsync<ValorConfiguracion>(
            """
            SELECT clave, valor, vigente_desde, (empresa_id IS NOT NULL) AS es_de_empresa
            FROM configuracion
            WHERE (empresa_id = @empresaId OR empresa_id IS NULL)
              AND activo = 1
              AND vigente_desde <= @fecha
              AND (vigente_hasta IS NULL OR vigente_hasta >= @fecha)
            """, new { empresaId, fecha }, ct);

    public async Task EstablecerAsync(
        long empresaId, string clave, string? valor, string tipoDato, DateOnly hoy, long usuarioId, DateTime ahoraUtc, CancellationToken ct)
    {
        var ayer = hoy.AddDays(-1);

        // Se cierra el valor anterior (sin borrarlo: queda el historial de vigencias).
        await EjecutarAsync(
            """
            UPDATE configuracion
            SET vigente_hasta = @ayer, version_fila = version_fila + 1, modificado_en = @ahoraUtc, modificado_por = @usuarioId
            WHERE empresa_id = @empresaId AND clave = @clave AND vigente_hasta IS NULL AND vigente_desde < @hoy
            """, new { empresaId, clave, hoy, ayer, ahoraUtc, usuarioId }, ct);

        var delDia = await ConsultarUnoAsync<long?>(
            "SELECT id FROM configuracion WHERE empresa_id = @empresaId AND clave = @clave AND vigente_desde = @hoy" + ParaActualizar,
            new { empresaId, clave, hoy }, ct);

        if (delDia is { } id)
        {
            // Mismo día: se reutiliza la fila (UNIQUE empresa, clave, vigente_desde). Sin valor = inactiva.
            await EjecutarAsync(
                """
                UPDATE configuracion
                SET valor = @valorFila, activo = @activo, vigente_hasta = NULL, version_fila = version_fila + 1,
                    modificado_en = @ahoraUtc, modificado_por = @usuarioId
                WHERE id = @id
                """, new { id, valorFila = valor ?? "", activo = valor is not null, ahoraUtc, usuarioId }, ct);
            return;
        }

        if (valor is null)
            return;

        await EjecutarAsync(
            """
            INSERT INTO configuracion (empresa_id, clave, valor, tipo_dato, vigente_desde, creado_en, creado_por)
            VALUES (@empresaId, @clave, @valor, @tipoDato, @hoy, @ahoraUtc, @usuarioId)
            """, new { empresaId, clave, valor, tipoDato, hoy, ahoraUtc, usuarioId }, ct);
    }
}

// ===== Referencias entre catálogos =====

public sealed class ReferenciasRepository(UnitOfWork unidadDeTrabajo) : RepositorioBase(unidadDeTrabajo), IReferencias
{
    // Los nombres de tabla son constantes de este archivo; nunca vienen de la solicitud.
    private static readonly Dictionary<EntidadReferencia, (string Tabla, bool PorEmpresa)> Entidades = new()
    {
        [EntidadReferencia.Sucursal] = ("sucursal", true),
        [EntidadReferencia.Bodega] = ("bodega", true),
        [EntidadReferencia.Caja] = ("caja", true),
        [EntidadReferencia.Departamento] = ("departamento", true),
        [EntidadReferencia.Banco] = ("banco", true),
        [EntidadReferencia.Moneda] = ("moneda", false),
        [EntidadReferencia.TipoDocumento] = ("tipo_documento", false),
    };

    private static readonly Dictionary<RelacionDependiente, string> Relaciones = new()
    {
        [RelacionDependiente.BodegasDeSucursal] = "SELECT COUNT(*) FROM bodega WHERE sucursal_id = @padreId AND activo = 1",
        [RelacionDependiente.CajasDeSucursal] = "SELECT COUNT(*) FROM caja WHERE sucursal_id = @padreId AND activo = 1",
        [RelacionDependiente.CajasDeBodega] = "SELECT COUNT(*) FROM caja WHERE bodega_id = @padreId AND activo = 1",
        [RelacionDependiente.CentrosCostoDeDepartamento] = "SELECT COUNT(*) FROM centro_costo WHERE departamento_id = @padreId AND activo = 1",
        [RelacionDependiente.CuentasDeBanco] = "SELECT COUNT(*) FROM cuenta_bancaria WHERE banco_id = @padreId AND activo = 1",
        [RelacionDependiente.CuentasDeMoneda] = "SELECT COUNT(*) FROM cuenta_bancaria WHERE moneda_id = @padreId AND activo = 1",
        [RelacionDependiente.EmpresasDeMonedaLocal] = "SELECT COUNT(*) FROM empresa WHERE moneda_local_id = @padreId AND activo = 1",
    };

    public async Task<bool> ExisteActivaAsync(EntidadReferencia entidad, long id, long empresaId, CancellationToken ct)
    {
        var (tabla, porEmpresa) = Entidades[entidad];
        var filtroEmpresa = porEmpresa ? " AND empresa_id = @empresaId" : "";
        // FOR SHARE: una baja concurrente del padre espera a que termine esta transacción.
        var sql = $"SELECT COUNT(*) FROM {tabla} WHERE id = @id AND activo = 1{filtroEmpresa}{(EnTransaccion ? " FOR SHARE" : "")}";
        return await EscalarAsync<long>(sql, new { id, empresaId }, ct) > 0;
    }

    public Task<long?> SucursalDeAsync(EntidadReferencia entidad, long id, long empresaId, CancellationToken ct)
    {
        var tabla = entidad switch
        {
            EntidadReferencia.Bodega => "bodega",
            EntidadReferencia.Caja => "caja",
            _ => throw new ArgumentOutOfRangeException(nameof(entidad), entidad, "Solo bodega y caja pertenecen a una sucursal."),
        };

        return ConsultarUnoAsync<long?>($"SELECT sucursal_id FROM {tabla} WHERE id = @id AND empresa_id = @empresaId", new { id, empresaId }, ct);
    }

    public Task<long> ContarDependientesActivosAsync(RelacionDependiente relacion, long padreId, CancellationToken ct) =>
        EscalarAsync<long>(Relaciones[relacion], new { padreId }, ct);
}
