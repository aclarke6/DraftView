using DraftView.Web.Services;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace DraftView.Integration.Tests.Email;

[Trait("Category", "Integration")]

public class SmtpEmailSenderIntegrationTests
{
    
    private static IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"]     = Environment.GetEnvironmentVariable("SMTP_HOST"),
            ["Email:Smtp:Port"]     = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587",
            ["Email:Smtp:Username"] = Environment.GetEnvironmentVariable("SMTP_USERNAME"),
            ["Email:Smtp:Password"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
            ["Email:Smtp:From"]     = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "noreply@draftview.co.uk",
            ["Email:Smtp:FromName"] = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "DraftView",
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact(Skip = "Integration test working")]
    public async Task SendAsync_SendsEmailViaSmtp_DoesNotThrow()
    {
        var host     = Environment.GetEnvironmentVariable("SMTP_HOST");
        var username = Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
            return;

        var config = BuildConfiguration();
        var sender = new SmtpEmailSender(config);

        var exception = await Record.ExceptionAsync(() =>
            sender.SendAsync(
                "ajclarke@myyahoo.com",
                "Alastair",
                "DraftView SMTP Integration Test",
                "<p>SMTP integration test from DraftView.</p>"));

        Assert.Null(exception);
    }
}
