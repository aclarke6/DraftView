using DraftView.Web.Services;
using Microsoft.Extensions.Configuration;

namespace DraftView.Integration.Tests.Email;

[Trait("Category", "Integration")]
public class SmtpEmailSenderIntegrationTests
{
    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DraftView.Web"))
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("0e437bf4-da42-4cf8-86cd-072126366d5c")
            .Build();
    }

    [Fact]
    public async Task SendAsync_SendsEmailViaSmtp_DoesNotThrow()
    {
        var config = BuildConfiguration();

        if (!string.Equals(config["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
            return;

        var sender = new SmtpEmailSender(config);

        var exception = await Record.ExceptionAsync(() =>
            sender.SendAsync(
                "ajclarke@myyahoo.com",
                "Alastair",
                "DraftView Integration Test",
                "<p>This is an integration test email from DraftView.</p>"));

        Assert.Null(exception);
    }
}
