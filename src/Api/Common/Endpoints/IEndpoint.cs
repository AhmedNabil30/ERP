using System.Reflection;
using Kaff.Api.Common.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Common.Endpoints;

/// <summary>
/// One vertical slice's route registration.
/// </summary>
/// <remarks>
/// CLAUDE.md fixes the slice layout: <c>Endpoint.cs · Handler.cs · Request.cs · Response.cs ·
/// Validator.cs</c> in one folder per feature. This interface is what <c>Endpoint.cs</c> implements.
/// Slices are discovered by scanning the assembly, so adding a feature never means editing a shared
/// registration file that two agents would then both be editing.
/// </remarks>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

/// <summary>Discovers and maps every <see cref="IEndpoint"/> in the Api assembly.</summary>
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapKaffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        IEnumerable<IEndpoint> endpoints = app.ServiceProvider.GetServices<IEndpoint>();

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.Map(app);
        }

        return app;
    }

    public static IServiceCollection AddKaffEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        Type[] concreteTypes =
        [
            .. assembly.GetExportedTypes().Where(type => type is { IsAbstract: false, IsInterface: false }),
        ];

        foreach (Type type in concreteTypes.Where(typeof(IEndpoint).IsAssignableFrom))
        {
            services.AddSingleton(typeof(IEndpoint), type);
        }

        // The slice's Validator.cs, discovered the same way and for the same reason.
        //
        // ValidationFilter resolves IRequestValidator<T> from the request scope and skips validation
        // silently when none is registered — so a validator that exists but was never registered is
        // an endpoint that quietly stopped validating. Scanning removes the step somebody forgets.
        foreach (Type type in concreteTypes)
        {
            foreach (Type contract in type.GetInterfaces()
                .Where(candidate => candidate.IsGenericType
                                    && candidate.GetGenericTypeDefinition() == typeof(IRequestValidator<>)))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }
}
