using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSyncControlFieldsToProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HeldUntilUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastBackgroundSyncOutcome",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulSyncUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAttemptUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWebhookUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SyncLeaseId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncLeaseExpiresUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncRequestedUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeldUntilUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastBackgroundSyncOutcome",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulSyncUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastSyncAttemptUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastWebhookUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SyncLeaseId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SyncLeaseExpiresUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SyncRequestedUtc",
                table: "Projects");
        }
    }
}
