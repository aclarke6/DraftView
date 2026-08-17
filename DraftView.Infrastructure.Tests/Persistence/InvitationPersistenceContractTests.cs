using System.IO;
using System.Linq;
using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class InvitationPersistenceContractTests
{
    private static DraftViewDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftViewDbContext(options);
    }

    [Fact]
    public void InvitationModel_UserIdIndex_Must_Not_Be_Unique()
    {
        using var db = CreateDb();
        var invitationEntity = db.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitationEntity);

        var userIdIndex = invitationEntity!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Invitation.UserId));

        Assert.NotNull(userIdIndex);
        Assert.False(userIdIndex!.IsUnique);
    }

    [Fact]
    public void AllowMultipleInvitationsPerUser_Migration_Must_Drop_Unique_UserId_Index()
    {
        var source = File.ReadAllText(Path.Combine(
            GetSolutionRoot(),
            "DraftView.Infrastructure",
            "Persistence",
            "Migrations",
            "20260415153000_AllowMultipleInvitationsPerUser.cs"));
        var upSectionStart = source.IndexOf("protected override void Up", StringComparison.Ordinal);
        var downSectionStart = source.IndexOf("protected override void Down", StringComparison.Ordinal);

        Assert.True(upSectionStart >= 0);
        Assert.True(downSectionStart > upSectionStart);

        var upSection = source.Substring(upSectionStart, downSectionStart - upSectionStart);

        Assert.Contains("DropIndex(", upSection, StringComparison.Ordinal);
        Assert.Contains("name: \"IX_Invitations_UserId\"", upSection, StringComparison.Ordinal);
        Assert.Contains("CreateIndex(", upSection, StringComparison.Ordinal);
        Assert.DoesNotContain("unique: true", upSection, StringComparison.Ordinal);
    }

    private static string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();

        while (dir != null &&
               !Directory.GetFiles(dir, "*.sln").Any() &&
               !Directory.GetFiles(dir, "*.slnx").Any())
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir == null)
            throw new InvalidOperationException("Solution root not found.");

        return dir;
    }
}
