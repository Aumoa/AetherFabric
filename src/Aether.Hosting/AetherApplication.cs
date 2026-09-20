using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aether.Hosting;

/// <summary>
/// Entry point for composing an Aether application on top of the .NET host.
/// </summary>
public static class AetherApplication
{
    public static AetherApplicationBuilder CreateBuilder(string[]? args = null)
    {
        return new AetherApplicationBuilder(args);
    }
}

public sealed class AetherApplicationBuilder
{
    private readonly HostApplicationBuilder _builder;

    internal AetherApplicationBuilder(string[]? args)
    {
        _builder = Host.CreateApplicationBuilder(args ?? []);
    }

    public IServiceCollection Services => _builder.Services;

    public IHost Build() => _builder.Build();
}
