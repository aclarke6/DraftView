using System.Text.Json;
using DraftView.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraftView.Infrastructure.Persistence;

public sealed class DraftViewDbContextFactory : IDesignTimeDbContextFactory<DraftViewDbContext>
{
    private const string WebUserSecretsId = "0e437bf4-da42-4cf8-86cd-072126366d5c";

    public DraftViewDbContext CreateDbContext(string[] args)
    {
        var webProjectRoot = FindWebProjectRoot();
        var appSettingsPath = Path.Combine(webProjectRoot, "appsettings.json");
        var userSecretsPath = FindUserSecretsPath();

        var connectionString = ReadConnectionString(appSettingsPath, userSecretsPath);
        var encryptionKey = ReadEmailProtectionKey("EmailProtection:EncryptionKey", userSecretsPath);
        var lookupHmacKey = ReadEmailProtectionKey("EmailProtection:LookupHmacKey", userSecretsPath);

        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DraftViewDbContext(
            options,
            new UserEmailEncryptionService(encryptionKey),
            new UserEmailLookupHmacService(lookupHmacKey));
    }

    private static string ReadConnectionString(string appSettingsPath, string? userSecretsPath)
    {
        var environmentValue = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        if (!string.IsNullOrWhiteSpace(userSecretsPath))
        {
            var userSecretsConnection = ReadJsonValue(userSecretsPath, "ConnectionStrings:DefaultConnection");
            if (!string.IsNullOrWhiteSpace(userSecretsConnection))
                return userSecretsConnection;
        }

        var appSettingsConnection = ReadJsonValue(appSettingsPath, "ConnectionStrings:DefaultConnection");
        if (!string.IsNullOrWhiteSpace(appSettingsConnection))
            return appSettingsConnection;

        throw new InvalidOperationException(
            "DefaultConnection was not found in environment variables, DraftView.Web user secrets, or DraftView.Web/appsettings.json.");
    }

    private static byte[] ReadEmailProtectionKey(string keyPath, string? userSecretsPath)
    {
        var environmentKey = Environment.GetEnvironmentVariable(keyPath.Replace(':', '_')) ??
                             Environment.GetEnvironmentVariable(keyPath.Replace(":", "__"));
        if (!string.IsNullOrWhiteSpace(environmentKey))
            return DecodeKey(environmentKey, keyPath);

        if (!string.IsNullOrWhiteSpace(userSecretsPath))
        {
            var secretValue = ReadJsonValue(userSecretsPath, keyPath);
            if (!string.IsNullOrWhiteSpace(secretValue))
                return DecodeKey(secretValue, keyPath);
        }

        throw new InvalidOperationException(
            $"Missing required configuration value '{keyPath}' for design-time DraftViewDbContext creation.");
    }

    private static byte[] DecodeKey(string configuredValue, string keyPath)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(configuredValue);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException(
                    $"Configuration value '{keyPath}' must decode to exactly 32 bytes.");

            return keyBytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Configuration value '{keyPath}' must be a valid base64-encoded 32-byte key.",
                ex);
        }
    }

    private static string? FindUserSecretsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var windowsPath = Path.Combine(appData, "Microsoft", "UserSecrets", WebUserSecretsId, "secrets.json");
            if (File.Exists(windowsPath))
                return windowsPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            var unixPath = Path.Combine(home, ".microsoft", "usersecrets", WebUserSecretsId, "secrets.json");
            if (File.Exists(unixPath))
                return unixPath;
        }

        return null;
    }

    private static string FindWebProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();

        while (dir is not null &&
               !Directory.GetFiles(dir, "*.sln").Any() &&
               !Directory.GetFiles(dir, "*.slnx").Any())
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir is null)
            throw new InvalidOperationException("Solution root not found.");

        return Path.Combine(dir, "DraftView.Web");
    }

    private static string? ReadJsonValue(string filePath, string keyPath)
    {
        if (!File.Exists(filePath))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(keyPath, out var flatValue))
        {
            return flatValue.ValueKind == JsonValueKind.String
                ? flatValue.GetString()
                : flatValue.ToString();
        }

        var current = root;

        foreach (var segment in keyPath.Split(':'))
        {
            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : current.ToString();
    }
}
