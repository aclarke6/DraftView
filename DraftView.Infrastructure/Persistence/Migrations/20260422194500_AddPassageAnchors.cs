using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPassageAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PassageAnchorId",
                table: "Comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResumeAnchorId",
                table: "ReadEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PassageAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSectionVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalSelectedText = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalNormalizedSelectedText = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalSelectedTextHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalPrefixContext = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalSuffixContext = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalStartOffset = table.Column<int>(type: "integer", nullable: false),
                    OriginalEndOffset = table.Column<int>(type: "integer", nullable: false),
                    OriginalCanonicalContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalHtmlSelectorHint = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentTargetSectionVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentStartOffset = table.Column<int>(type: "integer", nullable: true),
                    CurrentEndOffset = table.Column<int>(type: "integer", nullable: true),
                    CurrentMatchedText = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentConfidenceScore = table.Column<int>(type: "integer", nullable: true),
                    CurrentMatchMethod = table.Column<string>(type: "text", nullable: true),
                    CurrentResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassageAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassageAnchors_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PassageAnchors_SectionVersions_OriginalSectionVersionId",
                        column: x => x.OriginalSectionVersionId,
                        principalTable: "SectionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PassageAnchorId",
                table: "Comments",
                column: "PassageAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadEvents_ResumeAnchorId",
                table: "ReadEvents",
                column: "ResumeAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageAnchors_CreatedByUserId",
                table: "PassageAnchors",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageAnchors_OriginalSectionVersionId",
                table: "PassageAnchors",
                column: "OriginalSectionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageAnchors_Purpose",
                table: "PassageAnchors",
                column: "Purpose");

            migrationBuilder.CreateIndex(
                name: "IX_PassageAnchors_SectionId",
                table: "PassageAnchors",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_PassageAnchors_PassageAnchorId",
                table: "Comments",
                column: "PassageAnchorId",
                principalTable: "PassageAnchors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadEvents_PassageAnchors_ResumeAnchorId",
                table: "ReadEvents",
                column: "ResumeAnchorId",
                principalTable: "PassageAnchors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_PassageAnchors_PassageAnchorId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadEvents_PassageAnchors_ResumeAnchorId",
                table: "ReadEvents");

            migrationBuilder.DropTable(
                name: "PassageAnchors");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PassageAnchorId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_ReadEvents_ResumeAnchorId",
                table: "ReadEvents");

            migrationBuilder.DropColumn(
                name: "PassageAnchorId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ResumeAnchorId",
                table: "ReadEvents");
        }
    }
}
