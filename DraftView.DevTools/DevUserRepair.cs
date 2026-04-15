using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DraftView.DevTools;

public static class DevUserRepair
{
    private const string WebUserSecretsId = "0e437bf4-da42-4cf8-86cd-072126366d5c";

    private static readonly RepairSpec[] Repairs =
    [
        new("AJ Clarke - Author", "ajclarke@myyahoo.com", "AJ Clarke - Author", "ajclarke@myyahoo.com"),
        new("Becca Dunlop", "becca@draftview.local", "Becca Dunlop", "becca@draftview.local"),
        new("DraftView Support", "support@draftview.co.uk", "DraftView Support", "support@draftview.co.uk"),
        new("Hilary Royston-Bishop", "hilary@draftview.local", "Hilary Royston-Bishop", "hilary@draftview.local"),
        new("Pending", "test4@gmail.com", "Test 4 Reader", "test4@gmail.com"),
        new("reader 3", "test3@gmail.com", "Test 3 Reader", "test3@gmail.com"),
        new("Test 1 Reader", "test1@gmail.com", "Test 1 Reader", "test1@gmail.com"),
        new("Test 2 Reader", "test2@gmail.com", "Test 2 Reader", "test2@gmail.com")
    ];

    public static async Task<int> RunAsync()
    {
        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection was not found for DraftView.Web.");

        var encryptionKey = ReadEmailProtectionKey(configuration, "EmailProtection:EncryptionKey");
        var lookupHmacKey = ReadEmailProtectionKey(configuration, "EmailProtection:LookupHmacKey");

        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new DraftViewDbContext(
            options,
            new UserEmailEncryptionService(encryptionKey),
            new UserEmailLookupHmacService(lookupHmacKey));

        var repairErrors = new List<string>();

        foreach (var repair in Repairs)
        {
            var user = await FindDomainUserAsync(db, repair);
            if (user is null)
            {
                repairErrors.Add($"Domain user not found for '{repair.CurrentDisplayName}'.");
                continue;
            }

            user.UpdateDisplayName(repair.DesiredDisplayName);
            user.UpdateEmail(repair.DesiredEmail);

            var identityUser = await FindIdentityUserAsync(db, user, repair);
            if (identityUser is not null)
            {
                identityUser.UserName = repair.DesiredEmail;
                identityUser.NormalizedUserName = repair.DesiredEmail.ToUpperInvariant();
                identityUser.Email = repair.DesiredEmail;
                identityUser.NormalizedEmail = repair.DesiredEmail.ToUpperInvariant();
            }

            Console.WriteLine(
                $"Prepared: {repair.CurrentDisplayName} -> {repair.DesiredDisplayName} <{repair.DesiredEmail}>");
        }

        if (repairErrors.Count > 0)
        {
            Console.WriteLine("ERROR: repair aborted.");
            foreach (var error in repairErrors)
                Console.WriteLine("  - " + error);

            return 1;
        }

        await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("Dev user repair complete.");
        Console.WriteLine("Protected email state has been rebuilt under the configured fixed keys.");
        Console.WriteLine("Identity email/login values were updated where matching identity users were found.");

        return 0;
    }

    private static async Task<DraftView.Domain.Entities.User?> FindDomainUserAsync(
        DraftViewDbContext db,
        RepairSpec repair)
    {
        var currentDisplayName = repair.CurrentDisplayName.ToUpperInvariant();
        var desiredDisplayName = repair.DesiredDisplayName.ToUpperInvariant();

        return await db.AppUsers.FirstOrDefaultAsync(u =>
            u.DisplayName.ToUpper() == currentDisplayName ||
            u.DisplayName.ToUpper() == desiredDisplayName);
    }

    private static async Task<IdentityUser?> FindIdentityUserAsync(
        DraftViewDbContext db,
        DraftView.Domain.Entities.User user,
        RepairSpec repair)
    {
        return await db.Users.FirstOrDefaultAsync(i =>
            i.Id == user.Id.ToString() ||
            i.Email == repair.CurrentEmail ||
            i.Email == repair.DesiredEmail);
    }

    private static IConfiguration BuildConfiguration()
    {
        var webProjectRoot = FindWebProjectRoot();
        var userSecretsPath = FindUserSecretsPath();

        var builder = new ConfigurationBuilder()
            .SetBasePath(webProjectRoot)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        if (userSecretsPath is not null)
            builder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: false);

        return builder.Build();
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

    private static byte[] ReadEmailProtectionKey(IConfiguration configuration, string configPath)
    {
        var configuredValue = configuration[configPath];
        if (string.IsNullOrWhiteSpace(configuredValue))
            throw new InvalidOperationException(
                $"Missing required configuration value '{configPath}'.");

        try
        {
            var keyBytes = Convert.FromBase64String(configuredValue);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException(
                    $"Configuration value '{configPath}' must decode to exactly 32 bytes.");

            return keyBytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Configuration value '{configPath}' must be a valid base64-encoded 32-byte key.",
                ex);
        }
    }

    private sealed record RepairSpec(
        string CurrentDisplayName,
        string CurrentEmail,
        string DesiredDisplayName,
        string DesiredEmail);
}
