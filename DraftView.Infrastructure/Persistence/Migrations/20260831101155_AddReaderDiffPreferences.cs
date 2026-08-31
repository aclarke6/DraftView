using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderDiffPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiffCooldownHours",
                table: "UserPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<string>(
                name: "ReadingStyle",
                table: "UserPreferences",
                type: "text",
                nullable: false,
                defaultValue: "StoryReader");

            migrationBuilder.AddColumn<bool>(
                name: "ShowDiffOnRevisit",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMarkedReadAt",
                table: "ReadEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousReadVersionNumber",
                table: "ReadEvents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiffCooldownHours",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReadingStyle",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ShowDiffOnRevisit",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "LastMarkedReadAt",
                table: "ReadEvents");

            migrationBuilder.DropColumn(
                name: "PreviousReadVersionNumber",
                table: "ReadEvents");
        }
    }
}
