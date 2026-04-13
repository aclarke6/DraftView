using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedEmailPersistenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailCiphertext",
                table: "AppUsers",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailLookupHmac",
                table: "AppUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AppUsers"
                SET
                    "EmailCiphertext" = 'PENDING-CIPHERTEXT:' || "Id"::text,
                    "EmailLookupHmac" = 'PENDING-HMAC:' || "Id"::text
                WHERE "EmailCiphertext" IS NULL
                   OR "EmailLookupHmac" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "EmailCiphertext",
                table: "AppUsers",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailLookupHmac",
                table: "AppUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_EmailLookupHmac",
                table: "AppUsers",
                column: "EmailLookupHmac",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_EmailLookupHmac",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailCiphertext",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailLookupHmac",
                table: "AppUsers");
        }
    }
}
