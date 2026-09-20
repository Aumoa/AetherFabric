using Aether.Hosting;

namespace Aether.Hosting.Tests;

public sealed class AetherApplicationTests
{
    [Fact]
    public void CreateBuilder_ProvidesHostServices()
    {
        var builder = AetherApplication.CreateBuilder();

        Assert.NotNull(builder.Services);
    }
}
