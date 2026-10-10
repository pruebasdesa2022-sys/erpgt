using System.Reflection;
using ERP.Application.Catalogos;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Catalogos;

public static class RegistroCatalogos
{
    /// <summary>
    /// Registra los repositorios del motor de catálogos (toda clase que implemente
    /// <see cref="ICatalogoRepository{TDto, TCrear, TEditar}"/>), las reglas por defecto y las
    /// específicas (<see cref="IReglasCatalogo{TDto, TCrear, TEditar}"/> implementadas en Application),
    /// y los validadores base de los DTO que no declaran uno propio.
    /// </summary>
    public static IServiceCollection AddCatalogos(this IServiceCollection servicios)
    {
        // Reglas: sin reglas por defecto; las clases concretas de Application, si existen, las reemplazan.
        servicios.AddScoped(typeof(IReglasCatalogo<,,>), typeof(SinReglasCatalogo<,,>));

        foreach (var tipo in typeof(ICatalogoRepository<,,>).Assembly.GetTypes().Concat(typeof(RegistroCatalogos).Assembly.GetTypes()))
        {
            if (!tipo.IsClass || tipo.IsAbstract || tipo.ContainsGenericParameters)
                continue;

            foreach (var contrato in tipo.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definicion = contrato.GetGenericTypeDefinition();
                if (definicion == typeof(ICatalogoRepository<,,>) || definicion == typeof(IReglasCatalogo<,,>))
                    servicios.AddScoped(contrato, tipo);
            }
        }

        RegistrarValidadoresBase(servicios, typeof(CrearCatalogo), typeof(CrearCatalogoValidator<>));
        RegistrarValidadoresBase(servicios, typeof(EditarCatalogo), typeof(EditarCatalogoValidator<>));
        return servicios;
    }

    private static void RegistrarValidadoresBase(IServiceCollection servicios, Type baseDto, Type validadorGenerico)
    {
        var ensamblado = baseDto.Assembly;
        var conValidador = ensamblado.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(FluentValidation.IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        foreach (var dto in ensamblado.GetTypes().Where(t => t.IsClass && !t.IsAbstract && baseDto.IsAssignableFrom(t)))
        {
            if (conValidador.Contains(dto))
                continue;

            var validador = validadorGenerico.MakeGenericType(dto);
            servicios.AddScoped(typeof(FluentValidation.IValidator<>).MakeGenericType(dto), validador);
        }
    }
}
