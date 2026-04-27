using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DraftView.Domain.Interfaces.Services;
using DraftView.Application.Services;
using DraftView.Web.Extensions;
using Xunit;

namespace DraftView.Web.Tests;

/// <summary>
/// Tests service registration performed by web-layer service collection extensions.
/// Covers: authorization policy registration and human override service DI wiring.
/// Excludes: controller behaviour and application-service logic.
/// </summary>
public class AuthorizationPolicyRegistrationTests
{
    [Fact]
    public async Task AddIdentityServices_RegistersPolicies()
    {
        var services = new ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

        services.AddSingleton(configuration);

        // Register the application's service extensions that register Identity & policies
        services.AddPersistenceServices(configuration);
        services.AddConfiguredSettings(configuration);
        services.AddIdentityServices();

        var provider = services.BuildServiceProvider();
        var authOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>();

        var policyProvider = provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        var authorPolicy = await policyProvider.GetPolicyAsync("RequireAuthorPolicy");
        var readerPolicy = await policyProvider.GetPolicyAsync("RequireBetaReaderPolicy");

        Assert.NotNull(authorPolicy);
        Assert.NotNull(readerPolicy);
    }

    [Fact]
    public void AddApplicationServices_RegistersHumanOverrideService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["EmailProtection:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
                ["EmailProtection:LookupHmacKey"] = Convert.ToBase64String(new byte[32])
            }).Build();

        services.AddApplicationServices(configuration);

        var registration = Assert.Single(services, sd => sd.ServiceType == typeof(IHumanOverrideService));

        Assert.Equal(typeof(HumanOverrideService), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }
}
