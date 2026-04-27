using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DraftView.Infrastructure.Tests.Migrations;

/// <summary>
/// Tests EF Core migration source structure.
/// Covers: non-designer migration classes deriving from Migration and non-empty Up methods.
/// Excludes: migration designer files and production migration behaviour.
/// </summary>
public class MigrationStructureTests
{
    private const string LegacyEmptyUpMigrationFileName = "20260427121437_AddPassageAnchorFields.cs";

    [Fact]
    public void Migrations_Deriving_From_Migration_Must_Have_NonEmpty_Up_Methods()
    {
        var emptyUpMigrationFiles = Directory
            .EnumerateFiles(GetMigrationsDirectory(), "*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => Path.GetFileName(path) != "DraftViewDbContextModelSnapshot.cs")
            .Where(ContainsMigrationClassWithMissingOrEmptyUpMethod)
            // Legacy exception: this already-applied empty migration was superseded by
            // 20260427123533_ApplyMissingPassageAnchorRejectionAudit and must not fail the suite.
            .Where(path => Path.GetFileName(path) != LegacyEmptyUpMigrationFileName)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            emptyUpMigrationFiles.Length == 0,
            $"Migration files with missing or empty Up methods: {string.Join(", ", emptyUpMigrationFiles)}");
    }

    /// <summary>
    /// Parses one migration file and returns true when any class deriving from Migration
    /// has no Up method or an Up method without executable statements.
    /// </summary>
    private static bool ContainsMigrationClassWithMissingOrEmptyUpMethod(string migrationFilePath)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(migrationFilePath));
        var root = syntaxTree.GetCompilationUnitRoot();

        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(InheritsFromMigration)
            .Any(HasMissingOrEmptyUpMethod);
    }

    /// <summary>
    /// Returns true when the class base list contains Migration, including qualified names.
    /// </summary>
    private static bool InheritsFromMigration(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.BaseList?.Types.Any(baseType =>
            baseType.Type.ToString() == "Migration" ||
            baseType.Type.ToString().EndsWith(".Migration", StringComparison.Ordinal)) == true;
    }

    /// <summary>
    /// Returns true when no Up method exists or when its body contains no executable statements.
    /// </summary>
    private static bool HasMissingOrEmptyUpMethod(ClassDeclarationSyntax classDeclaration)
    {
        var upMethod = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method => method.Identifier.ValueText == "Up");

        if (upMethod == null)
            return true;

        if (upMethod.ExpressionBody != null)
            return false;

        return upMethod.Body == null || upMethod.Body.Statements.Count == 0;
    }

    /// <summary>
    /// Resolves the repository migrations directory from the current test execution location.
    /// </summary>
    private static string GetMigrationsDirectory()
    {
        var directory = Directory.GetCurrentDirectory();

        while (directory != null &&
               !Directory.GetFiles(directory, "*.sln").Any() &&
               !Directory.GetFiles(directory, "*.slnx").Any())
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (directory == null)
            throw new InvalidOperationException("Solution root not found.");

        return Path.Combine(
            directory,
            "DraftView.Infrastructure",
            "Persistence",
            "Migrations");
    }
}
