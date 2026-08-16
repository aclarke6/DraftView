using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenBookDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReaderBio",
                table: "UserPreferences",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaderGenreInterests",
                table: "UserPreferences",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReaderPace",
                table: "UserPreferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Brief",
                table: "Projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpen",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeenByReaderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessRequests_AppUsers_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_ProjectId",
                table: "AccessRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_ReaderId_ProjectId",
                table: "AccessRequests",
                columns: new[] { "ReaderId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "ReaderBio",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReaderGenreInterests",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReaderPace",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Brief",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "Projects");
        }
    }
}
