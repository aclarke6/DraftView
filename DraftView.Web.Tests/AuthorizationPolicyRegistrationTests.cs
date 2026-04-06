using Microsoft.Extensions.DependencyInjection;
using DraftView.Web.Extensions;
using Xunit;
using System.Linq;

namespace DraftView.Web.Tests;

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
}
