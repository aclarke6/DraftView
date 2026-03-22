using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Web.Services;

public class ConsoleEmailSender : IEmailSender
{
    public Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var sep = new string('-', 60);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(sep);
        Console.WriteLine("  DEV EMAIL (not sent)");
        Console.WriteLine(sep);
        Console.ResetColor();
        Console.WriteLine($"  To:      {toName} <{toEmail}>");
        Console.WriteLine($"  Subject: {subject}");
        Console.WriteLine();
        Console.WriteLine(htmlBody);
        Console.WriteLine(sep);
        Console.WriteLine();
        return Task.CompletedTask;
    }
}
