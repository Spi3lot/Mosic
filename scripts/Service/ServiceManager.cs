using System;
using System.Text.Json;

using Godot;

using Microsoft.Extensions.DependencyInjection;

using Mosic.Scripts.GitHub;

using Refit;

namespace Mosic.Scripts.Service;

public partial class ServiceManager : Node
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static ServiceProvider Provider { get; private set; }

    static ServiceManager()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IInstaller, ArchiveInstaller>();

        services.AddRefitClient<IGitHubApi>(
                new RefitSettings(new SystemTextJsonContentSerializer(JsonSerializerOptions)))
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.github.com"));

        Provider = services.BuildServiceProvider();
    }

    public override void _ExitTree()
    {
        Provider.Dispose();
        Provider = null;
    }
}
