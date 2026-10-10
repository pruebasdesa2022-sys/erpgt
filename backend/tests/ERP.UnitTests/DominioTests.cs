using ERP.Domain.Seguridad;
using ERP.Infrastructure.Persistencia;
using ERP.Infrastructure.Seguridad;

namespace ERP.UnitTests;

public class ReglasModuloTests
{
    [Theory]
    [InlineData(EstadoModulo.LIBERADO, EstadoModulo.LIBERADO, EstadoModulo.LIBERADO)]
    [InlineData(EstadoModulo.LIBERADO, EstadoModulo.EN_PRUEBAS, EstadoModulo.EN_PRUEBAS)]
    [InlineData(EstadoModulo.EN_PRUEBAS, EstadoModulo.LIBERADO, EstadoModulo.EN_PRUEBAS)]
    [InlineData(EstadoModulo.LIBERADO, EstadoModulo.SUSPENDIDO, EstadoModulo.SUSPENDIDO)]
    [InlineData(EstadoModulo.NO_LIBERADO, EstadoModulo.LIBERADO, EstadoModulo.NO_LIBERADO)]
    [InlineData(EstadoModulo.EN_DESARROLLO, EstadoModulo.EN_PRUEBAS, EstadoModulo.EN_DESARROLLO)]
    public void El_estado_efectivo_es_el_mas_restrictivo(EstadoModulo global, EstadoModulo empresa, EstadoModulo esperado) =>
        Assert.Equal(esperado, ReglasModulo.EstadoEfectivo(global, empresa));

    [Fact]
    public void Sin_estado_para_la_empresa_el_modulo_no_esta_liberado() =>
        Assert.Equal(EstadoModulo.NO_LIBERADO, ReglasModulo.EstadoEfectivo(EstadoModulo.LIBERADO, null));

    [Theory]
    [InlineData(EstadoModulo.LIBERADO, AccesoModulo.Disponible)]
    [InlineData(EstadoModulo.EN_PRUEBAS, AccesoModulo.SoloPruebas)]
    [InlineData(EstadoModulo.EN_DESARROLLO, AccesoModulo.Bloqueado)]
    [InlineData(EstadoModulo.NO_LIBERADO, AccesoModulo.Bloqueado)]
    [InlineData(EstadoModulo.SUSPENDIDO, AccesoModulo.Bloqueado)]
    [InlineData(EstadoModulo.RETIRADO, AccesoModulo.Bloqueado)]
    public void Solo_liberado_y_en_pruebas_dan_acceso(EstadoModulo estado, AccesoModulo esperado) =>
        Assert.Equal(esperado, ReglasModulo.Acceso(estado));

    [Fact]
    public void Un_modulo_base_solo_puede_estar_liberado()
    {
        Assert.True(ReglasModulo.CambioPermitido(esBase: true, EstadoModulo.LIBERADO));
        Assert.False(ReglasModulo.CambioPermitido(esBase: true, EstadoModulo.SUSPENDIDO));
        Assert.True(ReglasModulo.CambioPermitido(esBase: false, EstadoModulo.SUSPENDIDO));
    }
}

public class ResolucionPermisosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static PermisoDirecto Directo(string codigo, EfectoPermiso efecto, DateTime? hasta = null) =>
        new(codigo, efecto, Ahora.AddDays(-1), hasta);

    [Fact]
    public void Sin_permisos_directos_valen_los_de_los_roles() =>
        Assert.Equal(["A", "B"], ResolucionPermisos.Resolver(["A", "B"], [], Ahora).Order());

    [Fact]
    public void La_concesion_directa_agrega_un_permiso_que_los_roles_no_dan() =>
        Assert.Contains("C", ResolucionPermisos.Resolver(["A"], [Directo("C", EfectoPermiso.CONCEDER)], Ahora));

    [Fact]
    public void La_denegacion_directa_gana_a_los_roles() =>
        Assert.DoesNotContain("A", ResolucionPermisos.Resolver(["A", "B"], [Directo("A", EfectoPermiso.DENEGAR)], Ahora));

    [Fact]
    public void La_denegacion_gana_a_la_concesion_directa()
    {
        var efectivos = ResolucionPermisos.Resolver([], [Directo("A", EfectoPermiso.CONCEDER), Directo("A", EfectoPermiso.DENEGAR)], Ahora);
        Assert.Empty(efectivos);
    }

    [Fact]
    public void Un_permiso_directo_vencido_se_ignora()
    {
        var vencido = Ahora.AddMinutes(-1);
        Assert.Contains("A", ResolucionPermisos.Resolver(["A"], [Directo("A", EfectoPermiso.DENEGAR, vencido)], Ahora));
        Assert.DoesNotContain("C", ResolucionPermisos.Resolver(["A"], [Directo("C", EfectoPermiso.CONCEDER, vencido)], Ahora));
    }

    [Fact]
    public void Un_permiso_directo_que_aun_no_inicia_se_ignora()
    {
        var futuro = new PermisoDirecto("C", EfectoPermiso.CONCEDER, Ahora.AddDays(1), null);
        Assert.DoesNotContain("C", ResolucionPermisos.Resolver([], [futuro], Ahora));
    }
}

public class PoliticaPasswordTests
{
    private static readonly PoliticaPassword Politica = new(10, true, true, true, true, 5, 90, 5, 15, 30);

    [Fact]
    public void Acepta_una_contrasena_que_cumple_todo() => Assert.Empty(Politica.Validar("Cambiar.2026!"));

    [Theory]
    [InlineData("Corta.1a")]
    [InlineData("sinmayuscula.2026")]
    [InlineData("SINMINUSCULA.2026")]
    [InlineData("SinNumeros.Aqui")]
    [InlineData("SinSimbolo2026abc")]
    [InlineData(null)]
    public void Rechaza_lo_que_incumple_la_politica(string? password) => Assert.NotEmpty(Politica.Validar(password));

    [Fact]
    public void Rechaza_contrasenas_mas_largas_que_el_maximo() =>
        Assert.NotEmpty(Politica.Validar("Aa1!" + new string('x', PoliticaPassword.LongitudMaxima)));

    [Fact]
    public void Expira_cuando_pasan_los_dias_configurados()
    {
        var ahora = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(Politica.Expirada(ahora.AddDays(-90), ahora));
        Assert.False(Politica.Expirada(ahora.AddDays(-89), ahora));
    }

    [Fact]
    public void Con_cero_dias_nunca_expira()
    {
        var sinExpiracion = Politica with { ExpiraDias = 0 };
        Assert.False(sinExpiracion.Expirada(DateTime.UtcNow.AddYears(-10), DateTime.UtcNow));
    }
}

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verifica_la_contrasena_con_la_que_se_genero_el_hash()
    {
        var hash = _hasher.Hash("Cambiar.2026!");
        Assert.StartsWith("pbkdf2-sha512$210000$", hash);
        Assert.True(_hasher.Verificar("Cambiar.2026!", hash));
        Assert.False(_hasher.Verificar("cambiar.2026!", hash));
    }

    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos() =>
        Assert.NotEqual(_hasher.Hash("Cambiar.2026!"), _hasher.Hash("Cambiar.2026!"));

    /// <summary>El hash sembrado en database.sql debe seguir siendo válido para la contraseña inicial documentada.</summary>
    [Fact]
    public void Verifica_el_hash_del_administrador_sembrado_en_database_sql() =>
        Assert.True(_hasher.Verificar("Cambiar.2026!",
            "pbkdf2-sha512$210000$Mb7BW9Tadnpz/2dJ4fs4/A==$GJ0s7esg+gjdUTmBns68aV6NeQOFbI3qeQ20jhJdaKw="));

    [Theory]
    [InlineData("")]
    [InlineData("!")]
    [InlineData("pbkdf2-sha512$210000$no-es-base64$tampoco")]
    [InlineData("otro$1$AAAA$AAAA")]
    public void Un_hash_mal_formado_nunca_verifica(string hash) => Assert.False(_hasher.Verificar("Cambiar.2026!", hash));
}

public class OrdenamientoTests
{
    private static readonly Dictionary<string, string> Permitidos = new(StringComparer.OrdinalIgnoreCase) { ["nombre"] = "s.nombre" };

    [Theory]
    [InlineData("nombre", "s.nombre ASC")]
    [InlineData("-nombre", "s.nombre DESC")]
    [InlineData("NOMBRE", "s.nombre ASC")]
    [InlineData(null, "id ASC")]
    [InlineData("", "id ASC")]
    public void Traduce_solo_campos_permitidos(string? sort, string esperado) =>
        Assert.Equal(esperado, Ordenamiento.Resolver(sort, Permitidos, "id ASC"));

    [Theory]
    [InlineData("password_hash")]
    [InlineData("nombre; DROP TABLE usuario")]
    [InlineData("1")]
    public void Un_campo_fuera_de_la_lista_blanca_usa_el_orden_por_defecto(string sort) =>
        Assert.Equal("id ASC", Ordenamiento.Resolver(sort, Permitidos, "id ASC"));

    [Fact]
    public void Escapa_los_comodines_de_la_busqueda() =>
        Assert.Equal("%50\\%\\_a\\\\%", Ordenamiento.PatronContiene("50%_a\\"));
}
