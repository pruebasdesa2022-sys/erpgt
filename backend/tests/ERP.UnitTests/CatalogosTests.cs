using ERP.Application.Catalogos;
using ERP.Application.Organizacion;
using ERP.Domain.Catalogos;

namespace ERP.UnitTests;

public class NitTests
{
    // Los dígitos verificadores de estos casos se calcularon a mano con el algoritmo de módulo 11.
    [Theory]
    [InlineData("1234567-9")]
    [InlineData("12345679")]
    [InlineData(" 1234567 - 9 ")]
    [InlineData("9-4")]
    [InlineData("5-1")]
    [InlineData("6-K")]
    [InlineData("6-k")]
    public void Acepta_un_NIT_con_digito_verificador_correcto(string nit) => Assert.True(Nit.EsValido(nit));

    [Theory]
    [InlineData("1234567-8")]
    [InlineData("1234567-K")]
    [InlineData("9-5")]
    [InlineData("ABC1234-5")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("CF")]
    public void Rechaza_un_NIT_con_digito_incorrecto_o_formato_invalido(string? nit) => Assert.False(Nit.EsValido(nit));

    [Fact]
    public void Consumidor_final_se_reconoce_sin_ser_un_NIT_valido()
    {
        Assert.True(Nit.EsConsumidorFinal("c/f".Replace("/", "")));
        Assert.True(Nit.EsConsumidorFinal(" CF "));
        Assert.False(Nit.EsConsumidorFinal("1234567-9"));
    }

    [Fact]
    public void Normaliza_quitando_guiones_y_espacios()
    {
        Assert.Equal("1234567K", Nit.Normalizar(" 1234567-k "));
        Assert.Null(Nit.Normalizar("  "));
    }
}

public class FechaNegocioTests
{
    [Fact]
    public void Guatemala_va_seis_horas_detras_de_UTC()
    {
        // 03:00 UTC del 10 es todavía el 9 a las 21:00 en Guatemala.
        Assert.Equal(new DateOnly(2026, 10, 9), FechaNegocio.Hoy(new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 10, 10), FechaNegocio.Hoy(new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 12, 31), FechaNegocio.Hoy(new DateTime(2027, 1, 1, 5, 59, 0, DateTimeKind.Utc)));
    }
}

public class VigenciasTests
{
    private static DateOnly D(int mes, int dia) => new(2026, mes, dia);

    [Theory]
    [InlineData(1, 1, 6, 30, 7, 1, 12, 31, false)] // consecutivos sin compartir día
    [InlineData(1, 1, 7, 1, 7, 1, 12, 31, true)]   // comparten el 1 de julio
    [InlineData(1, 1, 12, 31, 6, 1, 6, 30, true)]  // uno contiene al otro
    public void Detecta_el_traslape_de_rangos_cerrados(int m1, int d1, int m2, int d2, int m3, int d3, int m4, int d4, bool esperado) =>
        Assert.Equal(esperado, Vigencias.SeTraslapan(D(m1, d1), D(m2, d2), D(m3, d3), D(m4, d4)));

    [Fact]
    public void Una_vigencia_abierta_se_traslapa_con_cualquier_posterior()
    {
        Assert.True(Vigencias.SeTraslapan(D(1, 1), null, D(8, 1), null));
        Assert.True(Vigencias.SeTraslapan(D(1, 1), null, D(8, 1), D(8, 31)));
        Assert.False(Vigencias.SeTraslapan(D(8, 1), D(8, 31), D(1, 1), D(7, 31)));
    }

    [Fact]
    public void Contiene_incluye_los_dos_extremos()
    {
        Assert.True(Vigencias.Contiene(D(1, 1), D(1, 31), D(1, 1)));
        Assert.True(Vigencias.Contiene(D(1, 1), D(1, 31), D(1, 31)));
        Assert.False(Vigencias.Contiene(D(1, 1), D(1, 31), D(2, 1)));
        Assert.True(Vigencias.Contiene(D(1, 1), null, D(12, 31)));
    }

    [Fact]
    public void Un_impuesto_inactivo_nunca_esta_vigente()
    {
        var impuesto = new ImpuestoDto { Activo = false, VigenteDesde = D(1, 1) };
        Assert.False(impuesto.EstaVigente(D(6, 1)));
        Assert.True((impuesto with { Activo = true }).EstaVigente(D(6, 1)));
    }
}

public class GrafoConversionesTests
{
    private const long Doc = 1, Uni = 2, Kg = 3, G = 4, Lb = 5, Oz = 6;

    private static GrafoConversiones Grafo() => new(
    [
        new AristaConversion(Doc, Uni, 12m),
        new AristaConversion(Kg, G, 1000m),
        new AristaConversion(Kg, Lb, 2.204623m),
        new AristaConversion(Lb, Oz, 16m),
    ]);

    [Fact]
    public void Convierte_en_el_sentido_directo_y_en_el_inverso()
    {
        var grafo = Grafo();
        Assert.Equal(12m, grafo.Factor(Doc, Uni));
        Assert.Equal(1m / 12m, grafo.Factor(Uni, Doc));
    }

    [Fact]
    public void Compone_las_conversiones_a_lo_largo_de_un_camino()
    {
        var grafo = Grafo();

        // 1 kg = 1000 g y 1 kg = 2.204623 lb → 1 g = 0.002204623 lb.
        Assert.Equal(0.002204623m, decimal.Round(grafo.Factor(G, Lb)!.Value, 9));
        // 1 kg = 2.204623 lb = 35.273968 oz.
        Assert.Equal(35.273968m, grafo.Factor(Kg, Oz));
    }

    [Fact]
    public void Una_unidad_se_convierte_en_si_misma_con_factor_uno()
    {
        Assert.Equal(1m, Grafo().Factor(Kg, Kg));
        Assert.Equal(1m, new GrafoConversiones([]).Factor(Kg, Kg));
    }

    [Fact]
    public void Unidades_sin_camino_no_tienen_factor()
    {
        var grafo = Grafo();
        Assert.Null(grafo.Factor(Uni, Kg));
        Assert.False(grafo.EstanConectadas(Doc, Oz));
    }

    [Fact]
    public void Detecta_que_una_conversion_nueva_cerraria_un_ciclo_o_duplicaria_un_camino()
    {
        var grafo = Grafo();
        Assert.True(grafo.EstanConectadas(G, Oz));   // ya hay camino G → KG → LB → OZ
        Assert.True(grafo.EstanConectadas(Oz, G));   // y en sentido inverso
        Assert.False(grafo.EstanConectadas(Doc, Kg)); // componentes distintos: se puede unir
    }
}

public class FormatoNumeroDocumentoTests
{
    private static readonly DateOnly Fecha = new(2026, 10, 10);

    [Fact]
    public void Arma_prefijo_anio_serie_y_numero_con_ceros()
    {
        Assert.Equal("FAC-2026-A-000123", FormatoNumeroDocumento.Formatear("A", "FAC", true, 6, 123, Fecha));
        Assert.Equal("A-000123", FormatoNumeroDocumento.Formatear("A", null, false, 6, 123, Fecha));
        Assert.Equal("A-000123", FormatoNumeroDocumento.Formatear("A", "  ", false, 6, 123, Fecha));
    }

    [Fact]
    public void Un_numero_mas_largo_que_la_longitud_no_se_trunca() =>
        Assert.Equal("A-1234567", FormatoNumeroDocumento.Formatear("A", null, false, 6, 1_234_567, Fecha));
}

public class ValidadoresCatalogosTests
{
    [Fact]
    public void El_validador_base_exige_codigo_valido_y_nombre()
    {
        var validador = new CrearMarcaValidatorParaPruebas();

        Assert.True((validador.Validate(new CrearMarcaDto { Codigo = "SONY", Nombre = "Sony" })).IsValid);
        Assert.False(validador.Validate(new CrearMarcaDto { Codigo = "con espacio", Nombre = "Sony" }).IsValid);
        Assert.False(validador.Validate(new CrearMarcaDto { Codigo = "", Nombre = "Sony" }).IsValid);
        Assert.False(validador.Validate(new CrearMarcaDto { Codigo = "OK", Nombre = "" }).IsValid);
        Assert.False(validador.Validate(new CrearMarcaDto { Codigo = new string('A', 21), Nombre = "Sony" }).IsValid);
    }

    private sealed class CrearMarcaValidatorParaPruebas : CrearCatalogoValidator<CrearMarcaDto>;

    [Fact]
    public void La_moneda_exige_codigo_ISO_de_tres_letras()
    {
        var validador = new CrearMonedaValidator();
        Assert.True(validador.Validate(new CrearMonedaDto { Codigo = "EUR", Nombre = "Euro", Simbolo = "€" }).IsValid);
        Assert.False(validador.Validate(new CrearMonedaDto { Codigo = "EURO", Nombre = "Euro", Simbolo = "€" }).IsValid);
        Assert.False(validador.Validate(new CrearMonedaDto { Codigo = "E1R", Nombre = "Euro", Simbolo = "€" }).IsValid);
    }

    [Fact]
    public void La_empresa_exige_un_NIT_con_digito_verificador_correcto()
    {
        var validador = new EditarEmpresaValidator();
        EditarEmpresaDto Con(string nit) =>
            new("Razón", "Comercial", nit, null, null, null, "GENERAL", 1, 1);

        Assert.True(validador.Validate(Con("1234567-9")).IsValid);
        Assert.False(validador.Validate(Con("1234567-1")).IsValid);
        Assert.False(validador.Validate(Con("CF")).IsValid);
    }

    [Fact]
    public void El_tipo_de_cambio_admite_hasta_ocho_decimales_y_es_positivo()
    {
        var validador = new CrearTipoCambioValidator();
        var fecha = new DateOnly(2026, 10, 10);
        Assert.True(validador.Validate(new CrearTipoCambioDto(1, fecha, 7.65432101m)).IsValid);
        Assert.False(validador.Validate(new CrearTipoCambioDto(1, fecha, 7.654321012m)).IsValid);
        Assert.False(validador.Validate(new CrearTipoCambioDto(1, fecha, 0m)).IsValid);
        Assert.False(validador.Validate(new CrearTipoCambioDto(1, fecha, -1m)).IsValid);
    }

    [Fact]
    public void La_conversion_exige_unidades_distintas_y_factor_positivo_de_seis_decimales()
    {
        var validador = new CrearConversionUnidadValidator();
        Assert.True(validador.Validate(new CrearConversionUnidadDto(1, 2, 12.5m)).IsValid);
        Assert.False(validador.Validate(new CrearConversionUnidadDto(1, 1, 12m)).IsValid);
        Assert.False(validador.Validate(new CrearConversionUnidadDto(1, 2, 0m)).IsValid);
        Assert.False(validador.Validate(new CrearConversionUnidadDto(1, 2, 1.1234567m)).IsValid);
    }

    [Fact]
    public void La_serie_valida_longitud_rango_y_vigencia()
    {
        var validador = new CrearSerieDocumentoValidator();
        var bueno = new CrearSerieDocumentoDto
        {
            TipoDocumentoId = 1, Serie = "A", Longitud = 6, SiguienteCorrelativo = 1, NumeroMaximo = 1000, VigenteDesde = new DateOnly(2026, 1, 1),
        };

        Assert.True(validador.Validate(bueno).IsValid);
        Assert.False(validador.Validate(bueno with { Longitud = 0 }).IsValid);
        Assert.False(validador.Validate(bueno with { Longitud = 13 }).IsValid);
        Assert.False(validador.Validate(bueno with { SiguienteCorrelativo = 0 }).IsValid);
        Assert.False(validador.Validate(bueno with { SiguienteCorrelativo = 10, NumeroMaximo = 5 }).IsValid);
        Assert.False(validador.Validate(bueno with { VigenteHasta = new DateOnly(2025, 12, 31) }).IsValid);
        Assert.False(validador.Validate(bueno with { Serie = "A-1" }).IsValid);
        Assert.False(validador.Validate(bueno with { CajaId = 3, SucursalId = null }).IsValid);
    }

    [Fact]
    public void El_impuesto_valida_porcentaje_y_vigencia()
    {
        var validador = new CrearImpuestoValidator();
        var bueno = new CrearImpuestoDto("IVA", "IVA 12 %", "IVA", 12m, new DateOnly(2026, 1, 1), null);

        Assert.True(validador.Validate(bueno).IsValid);
        Assert.False(validador.Validate(bueno with { Porcentaje = 100.5m }).IsValid);
        Assert.False(validador.Validate(bueno with { Porcentaje = -1m }).IsValid);
        Assert.False(validador.Validate(bueno with { Tipo = "ISR" }).IsValid);
        Assert.False(validador.Validate(bueno with { VigenteHasta = new DateOnly(2025, 12, 31) }).IsValid);
    }
}
