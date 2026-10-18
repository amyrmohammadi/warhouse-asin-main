using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Common.Behaviors;

namespace Warehouse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        params Assembly[] additionalMappingAssemblies)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();
        var mappingAssemblies = additionalMappingAssemblies
            .Append(applicationAssembly)
            .Distinct()
            .ToArray();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(applicationAssembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddAutoMapper(_ => { }, mappingAssemblies);
        services.AddValidatorsFromAssembly(
            applicationAssembly,
            includeInternalTypes: true);

        return services;
    }
}
