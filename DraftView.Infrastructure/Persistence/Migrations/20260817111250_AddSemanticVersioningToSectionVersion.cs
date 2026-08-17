using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemanticVersioningToSectionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MajorVersion",
                table: "SectionVersions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinorVersion",
                table: "SectionVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScrivenerStatus",
                table: "SectionVersions",
                type: "text",
                nullable: true);

            // Backfill: treat all pre-existing versions as 1.00, 1.01, 1.02 ... per section.
            // MajorVersion stays 1; MinorVersion = VersionNumber - 1.
            migrationBuilder.Sql(
                """
                UPDATE "SectionVersions"
                SET "MajorVersion" = 1,
                    "MinorVersion" = "VersionNumber" - 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MajorVersion",
                table: "SectionVersions");

            migrationBuilder.DropColumn(
                name: "MinorVersion",
                table: "SectionVersions");

            migrationBuilder.DropColumn(
                name: "ScrivenerStatus",
                table: "SectionVersions");
        }
    }
}
