using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMTSprintForeignKeyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Tenancies_Accounts_OwnerAccountId",
                table: "Tenancies",
                column: "OwnerAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenancyMemberships_Accounts_AccountId",
                table: "TenancyMemberships",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenancyMemberships_Tenancies_TenancyId",
                table: "TenancyMemberships",
                column: "TenancyId",
                principalTable: "Tenancies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenancySubscriptions_Tenancies_TenancyId",
                table: "TenancySubscriptions",
                column: "TenancyId",
                principalTable: "Tenancies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenancies_Accounts_OwnerAccountId",
                table: "Tenancies");

            migrationBuilder.DropForeignKey(
                name: "FK_TenancyMemberships_Accounts_AccountId",
                table: "TenancyMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_TenancyMemberships_Tenancies_TenancyId",
                table: "TenancyMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_TenancySubscriptions_Tenancies_TenancyId",
                table: "TenancySubscriptions");
        }
    }
}
