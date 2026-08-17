using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DraftView.DevTools;

internal static class EmailTest
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var host     = GetArg(args, "--host")     ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var portStr  = GetArg(args, "--port")     ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
        var username = GetArg(args, "--username") ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = GetArg(args, "--password") ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var from     = GetArg(args, "--from")     ?? Environment.GetEnvironmentVariable("SMTP_FROM") ?? "noreply@draftview.co.uk";
        var to       = GetArg(args, "--to")       ?? Environment.GetEnvironmentVariable("SMTP_TO")   ?? "ajclarke@myyahoo.com";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: SMTP_HOST, SMTP_USERNAME, and SMTP_PASSWORD are required.");
            Console.WriteLine("       Set them as environment variables or pass --host/--username/--password.");
            Console.ResetColor();
            return 1;
        }

        if (!int.TryParse(portStr, out var port))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Invalid port '{portStr}'.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"  Host     : {host}:{port}");
        Console.WriteLine($"  From     : {from}");
        Console.WriteLine($"  To       : {to}");
        Console.WriteLine($"  Username : {username}");
        Console.WriteLine();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("DraftView", from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = "DraftView Email Test";
        message.Body = new TextPart("html") { Text = "<p>This is a test email from DraftView DevTools.</p>" };

        try
        {
            using var client = new SmtpClient();
            Console.WriteLine("  Connecting...");
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            Console.WriteLine("  Authenticating...");
            await client.AuthenticateAsync(username, password);
            Console.WriteLine("  Sending...");
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Email sent successfully.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAILED: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static string? GetArg(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }
}
