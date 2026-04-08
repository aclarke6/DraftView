using DraftView.Domain.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DraftView.Web.Services;

public class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var host = config["Email:Smtp:Host"] ?? "localhost";
        var port = int.Parse(config["Email:Smtp:Port"] ?? "587");
        var user = config["Email:Smtp:Username"] ?? string.Empty;
        var pass = config["Email:Smtp:Password"] ?? string.Empty;
        var fromAddr = config["Email:Smtp:From"] ?? "noreply@draftview.co.uk";
        var fromName = config["Email:Smtp:FromName"] ?? "DraftView";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddr));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(user, pass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}