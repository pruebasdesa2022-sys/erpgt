using System.Reflection;
using System.Text.RegularExpressions;
using ERP.Api.Seguridad;
using ERP.Application.Comun;
using ERP.Domain.Comun;
using ERP.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NetArchTest.Rules;

namespace ERP.UnitTests;

/// <summary>Hace cumplir las dependencias entre capas: Api → Application → Domain e Infrastructure → Application.</summary>
public class ArquitecturaTests
{
    private static readonly Assembly Dominio = typeof(ErpException).Assembly;
    private static readonly Assembly Aplicacion = typeof(IClock).Assembly;
    private static readonly Assembly Infraestructura = typeof(UnitOfWork).Assembly;

    [Fact]
    public void El_dominio_no_depende_de_ninguna_otra_capa_ni_de_paquetes()
    {
        var resultado = Types.InAssembly(Dominio).ShouldNot()
            .HaveDependencyOnAny("ERP.Application", "ERP.Infrastructure", "ERP.Api", "Dapper", "MySqlConnector", "Microsoft", "FluentValidation", "Serilog")
            .GetResult();
        Assert.True(resultado.IsSuccessful, Fallos(resultado));
    }

    [Fact]
    public void La_aplicacion_no_depende_de_infraestructura_api_ni_acceso_a_datos()
    {
        var resultado = Types.InAssembly(Aplicacion).ShouldNot()
            .HaveDependencyOnAny("ERP.Infrastructure", "ERP.Api", "Dapper", "MySqlConnector", "Microsoft.AspNetCore", "MailKit")
            .GetResult();
        Assert.True(resultado.IsSuccessful, Fallos(resultado));
    }

    [Fact]
    public void La_infraestructura_no_depende_de_la_api()
    {
        var resultado = Types.InAssembly(Infraestructura).ShouldNot().HaveDependencyOnAny("ERP.Api", "Microsoft.AspNetCore.Mvc").GetResult();
        Assert.True(resultado.IsSuccessful, Fallos(resultado));
    }

    [Fact]
    public void Los_controladores_no_usan_acceso_a_datos_directamente()
    {
        var resultado = Types.InAssembly(typeof(Program).Assembly)
            .That().Inherit(typeof(ControllerBase))
            .ShouldNot().HaveDependencyOnAny("Dapper", "MySqlConnector", "ERP.Infrastructure")
            .GetResult();
        Assert.True(resultado.IsSuccessful, Fallos(resultado));
    }

    private static string Fallos(TestResult resultado) =>
        "Tipos que incumplen: " + string.Join(", ", resultado.FailingTypeNames ?? []);
}

/// <summary>
/// "El backend nunca confía en el frontend": recorre todos los controladores y falla si algún
/// endpoint no declara explícitamente cómo se protege.
/// </summary>
public partial class ProteccionDeEndpointsTests
{
    /// <summary>Únicos endpoints que pueden ser anónimos. Agregar uno aquí es una decisión de seguridad.</summary>
    private static readonly HashSet<string> AnonimosPermitidos =
    [
        "AutenticacionController.Login",
        "AutenticacionController.Refresh",
        "AutenticacionController.CambiarContexto",
        "AutenticacionController.Logout",
        "AutenticacionController.SolicitarRecuperacion",
        "AutenticacionController.Restablecer",
    ];

    public static IEnumerable<object[]> Acciones() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => new object[] { m.DeclaringType!.Name, m.Name });

    [Fact]
    public void Hay_endpoints_que_revisar() => Assert.True(Acciones().Count() >= 20);

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Cada_endpoint_declara_exactamente_una_forma_de_proteccion(string controlador, string accion)
    {
        var metodo = Metodo(controlador, accion);
        var marcas = new[]
        {
            metodo.GetCustomAttribute<RequierePermisoAttribute>() is not null,
            metodo.GetCustomAttribute<SoloAutenticadoAttribute>() is not null,
            metodo.GetCustomAttribute<AllowAnonymousAttribute>() is not null,
        }.Count(m => m);

        Assert.True(marcas == 1,
            $"{controlador}.{accion} debe llevar exactamente uno de [RequierePermiso], [SoloAutenticado] o [AllowAnonymous].");
    }

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Solo_los_endpoints_de_la_lista_pueden_ser_anonimos(string controlador, string accion)
    {
        var esAnonimo = Metodo(controlador, accion).GetCustomAttribute<AllowAnonymousAttribute>() is not null;
        Assert.Equal(AnonimosPermitidos.Contains($"{controlador}.{accion}"), esAnonimo);
    }

    [Theory]
    [MemberData(nameof(Acciones))]
    public void El_codigo_de_permiso_sigue_el_patron_MODULO_RECURSO_ACCION(string controlador, string accion)
    {
        var permiso = Metodo(controlador, accion).GetCustomAttribute<RequierePermisoAttribute>();
        if (permiso is not null)
            Assert.Matches(PatronPermiso(), permiso.Codigo);
    }

    [Fact]
    public void Todos_los_controladores_llevan_Authorize_y_ninguno_AllowAnonymous_a_nivel_de_clase()
    {
        var controladores = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(controladores);
        Assert.All(controladores, c =>
        {
            Assert.True(c.GetCustomAttribute<AuthorizeAttribute>() is not null, $"{c.Name} debe llevar [Authorize].");
            Assert.True(c.GetCustomAttribute<AllowAnonymousAttribute>() is null, $"{c.Name} no puede llevar [AllowAnonymous] en la clase.");
        });
    }

    private static MethodInfo Metodo(string controlador, string accion) =>
        typeof(Program).Assembly.GetTypes().Single(t => t.Name == controlador).GetMethod(accion)!;

    [GeneratedRegex("^[A-Z]+_[A-Z]+_(VER|CREAR|EDITAR|PROCESAR|ANULAR|ELIMINAR|APROBAR|EXPORTAR|IMPRIMIR)$")]
    private static partial Regex PatronPermiso();
}
