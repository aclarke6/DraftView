using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "ReadEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReaderSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReaderSnapshots_SectionId",
                table: "ReaderSnapshots",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderSnapshots_SectionId_UserId",
                table: "ReaderSnapshots",
                columns: new[] { "SectionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReaderSnapshots");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "ReadEvents");
        }
    }
}
