using System.Net;
using System.Net.Mail;
using DraftView.Domain.Interfaces.Services;

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
        var host     = config["Email:Smtp:Host"]     ?? "localhost";
        var port     = int.Parse(config["Email:Smtp:Port"] ?? "587");
        var user     = config["Email:Smtp:Username"] ?? string.Empty;
        var pass     = config["Email:Smtp:Password"] ?? string.Empty;
        var fromAddr = config["Email:Smtp:From"]     ?? "noreply@draftview.co.uk";
        var fromName = config["Email:Smtp:FromName"] ?? "DraftView";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl   = true,
            Credentials = new NetworkCredential(user, pass)
        };

        var message = new MailMessage
        {
            From       = new MailAddress(fromAddr, fromName),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail, toName));

        await client.SendMailAsync(message, ct);
    }
}
