#nullable enable

using System.Net.Http;
using Bible.Alarm.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class ServiceRegistrationHelperTests
{
    [Fact]
    public void RegisterServices_builds_provider_and_resolves_core_singletons()
    {
        var services = new ServiceCollection();
        ServiceRegistrationHelper.RegisterServices(services);

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<HttpClient>();
        _ = provider.GetRequiredService<ILogger>();
    }
}
