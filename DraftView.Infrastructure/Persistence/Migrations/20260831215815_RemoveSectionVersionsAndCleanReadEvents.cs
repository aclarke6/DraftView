using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSectionVersionsAndCleanReadEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PassageAnchors_SectionVersions_OriginalSectionVersionId",
                table: "PassageAnchors");

            migrationBuilder.DropTable(
                name: "SectionVersions");

            migrationBuilder.DropIndex(
                name: "IX_PassageAnchors_OriginalSectionVersionId",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "BannerDismissedAtVersion",
                table: "ReadEvents");

            migrationBuilder.DropColumn(
                name: "LastReadVersionNumber",
                table: "ReadEvents");

            migrationBuilder.DropColumn(
                name: "PreviousReadVersionNumber",
                table: "ReadEvents");

            migrationBuilder.DropColumn(
                name: "CurrentTargetSectionVersionId",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "OriginalSectionVersionId",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "RejectedTargetSectionVersionId",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "SectionVersionId",
                table: "Comments");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                table: "ReadEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                table: "ReadEvents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BannerDismissedAtVersion",
                table: "ReadEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastReadVersionNumber",
                table: "ReadEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousReadVersionNumber",
                table: "ReadEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentTargetSectionVersionId",
                table: "PassageAnchors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalSectionVersionId",
                table: "PassageAnchors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedTargetSectionVersionId",
                table: "PassageAnchors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionVersionId",
                table: "Comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SectionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeClassification = table.Column<int>(type: "integer", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    MajorVersion = table.Column<int>(type: "integer", nullable: false),
                    MinorVersion = table.Column<int>(type: "integer", nullable: false),
                    ScrivenerStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassageAnchors_OriginalSectionVersionId",
                table: "PassageAnchors",
                column: "OriginalSectionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionVersions_SectionId",
                table: "SectionVersions",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PassageAnchors_SectionVersions_OriginalSectionVersionId",
                table: "PassageAnchors",
                column: "OriginalSectionVersionId",
                principalTable: "SectionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
