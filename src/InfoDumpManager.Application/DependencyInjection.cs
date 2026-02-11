using FluentValidation;
using InfoDumpManager.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfoDumpManager.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer services: MediatR, AutoMapper, FluentValidation, pipeline behaviors.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyReference).Assembly;

        services.AddAutoMapper(assembly);

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
        });

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
