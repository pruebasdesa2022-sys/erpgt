using ERP.Application.Auditoria;
using ERP.Application.Catalogos;
using ERP.Application.Comun;
using ERP.Application.Organizacion;
using ERP.Application.Seguridad;
using ERP.Application.Seguridad.Contratos;
using ERP.Infrastructure.Auditoria;
using ERP.Infrastructure.Catalogos;
using ERP.Infrastructure.Integraciones;
using ERP.Infrastructure.Organizacion;
using ERP.Infrastructure.Persistencia;
using ERP.Infrastructure.Seguridad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra los adaptadores: persistencia, seguridad, correo y reloj.</summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var jwt = configuracion.GetSection("Jwt").Get<OpcionesJwt>() ?? new OpcionesJwt();
        var correo = configuracion.GetSection("Correo").Get<OpcionesCorreo>() ?? new OpcionesCorreo();

        servicios.AddSingleton(jwt);
        servicios.AddSingleton(correo);
        // La cadena se lee al resolver, no al registrar, para que las pruebas puedan sustituirla.
        servicios.AddSingleton(sp => new OpcionesBaseDatos
        {
            CadenaConexion = sp.GetRequiredService<IConfiguration>().GetConnectionString("Erp")
                ?? throw new InvalidOperationException(
                    "Falta la cadena de conexión ConnectionStrings:Erp. Configúrela en User Secrets o en la variable de entorno ConnectionStrings__Erp."),
        });

        servicios.AddMemoryCache();
        servicios.AddSingleton<IClock, SystemClock>();
        servicios.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        servicios.AddSingleton<ITokenService, JwtTokenService>();

        if (string.Equals(correo.Modo, "Smtp", StringComparison.OrdinalIgnoreCase))
            servicios.AddSingleton<ICorreo, CorreoSmtp>();
        else
            servicios.AddSingleton<ICorreo, CorreoArchivo>();

        servicios.AddScoped<UnitOfWork>();
        servicios.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());

        servicios.AddScoped<AuditoriaRepository>();
        servicios.AddScoped<IAuditoria>(sp => sp.GetRequiredService<AuditoriaRepository>());
        servicios.AddScoped<IBitacoraRepository>(sp => sp.GetRequiredService<AuditoriaRepository>());

        servicios.AddScoped<EmpresaRepository>();
        servicios.AddScoped<IEmpresaRepository>(sp => sp.GetRequiredService<EmpresaRepository>());
        servicios.AddScoped<IOrganizacionConsulta>(sp => sp.GetRequiredService<EmpresaRepository>());
        servicios.AddScoped<ISucursalRepository, SucursalRepository>();

        // Etapa 1A: catálogos y numeración.
        servicios.AddCatalogos();
        servicios.AddScoped<IReferencias, ReferenciasRepository>();
        servicios.AddScoped<TipoCambioRepository>();
        servicios.AddScoped<ITipoCambioRepository>(sp => sp.GetRequiredService<TipoCambioRepository>());
        servicios.AddScoped<ITiposCambioConsulta>(sp => sp.GetRequiredService<TipoCambioRepository>());
        servicios.AddScoped<IImpuestoRepository, ImpuestoRepository>();
        servicios.AddScoped<IConversionUnidadRepository, ConversionUnidadRepository>();
        servicios.AddScoped<ISerieDocumentoRepository, SerieDocumentoRepository>();
        servicios.AddScoped<INumeracionRepository, NumeracionRepository>();
        servicios.AddScoped<IConfiguracionRepository, ConfiguracionRepository>();

        servicios.AddScoped<ICredencialesRepository, CredencialesRepository>();
        servicios.AddScoped<ISesionRepository, SesionRepository>();
        servicios.AddScoped<IAutorizacionRepository, AutorizacionRepository>();
        servicios.AddScoped<UsuarioRepository>();
        servicios.AddScoped<IUsuarioRepository>(sp => sp.GetRequiredService<UsuarioRepository>());
        servicios.AddScoped<IUsuariosConsulta>(sp => sp.GetRequiredService<UsuarioRepository>());
        servicios.AddScoped<IRolRepository, RolRepository>();
        servicios.AddScoped<IAccesosOperativosRepository, AccesosOperativosRepository>();
        servicios.AddScoped<ModuloRepository>();
        servicios.AddScoped<IModuloRepository>(sp => sp.GetRequiredService<ModuloRepository>());
        servicios.AddScoped<IMenuRepository>(sp => sp.GetRequiredService<ModuloRepository>());

        return servicios;
    }
}
