using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aether.Gateway;

public static class AetherGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddAetherGateway(
        this IServiceCollection services,
        Action<AetherGatewayOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<AetherGatewayOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<AetherGatewayServer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(
            provider => provider.GetRequiredService<AetherGatewayServer>()));

        return services;
    }

    public static IServiceCollection AddAetherGateway<TAuthenticator, TRouteResolver>(
        this IServiceCollection services,
        Action<AetherGatewayOptions>? configure = null)
        where TAuthenticator : class, IAetherGatewayAuthenticator
        where TRouteResolver : class, IAetherGatewayRouteResolver
    {
        services.TryAddSingleton<IAetherGatewayAuthenticator, TAuthenticator>();
        services.TryAddSingleton<IAetherGatewayRouteResolver, TRouteResolver>();
        return services.AddAetherGateway(configure);
    }
}
