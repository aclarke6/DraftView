using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPassageAnchorRejectionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "PassageAnchors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "PassageAnchors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedReason",
                table: "PassageAnchors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedTargetSectionVersionId",
                table: "PassageAnchors",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"PassageAnchors\" SET \"Status\" = 'UserRejected' WHERE \"Status\" = 'Rejected';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"PassageAnchors\" SET \"Status\" = 'Rejected' WHERE \"Status\" = 'UserRejected';");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "RejectedReason",
                table: "PassageAnchors");

            migrationBuilder.DropColumn(
                name: "RejectedTargetSectionVersionId",
                table: "PassageAnchors");
        }
    }
}
