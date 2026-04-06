using Microsoft.Extensions.DependencyInjection;
using DraftView.Web.Extensions;
using Xunit;
using System.Linq;

namespace DraftView.Web.Tests;

public class AuthorizationPolicyRegistrationTests
{
    [Fact]
    public void AddIdentityServices_RegistersPolicies()
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
        var authorPolicy = policyProvider.GetPolicyAsync("RequireAuthorPolicy").GetAwaiter().GetResult();
        var readerPolicy = policyProvider.GetPolicyAsync("RequireBetaReaderPolicy").GetAwaiter().GetResult();

        Assert.NotNull(authorPolicy);
        Assert.NotNull(readerPolicy);
    }
}
