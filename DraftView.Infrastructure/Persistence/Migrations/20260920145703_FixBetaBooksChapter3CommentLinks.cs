using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DraftView.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixBetaBooksChapter3CommentLinks : Migration
    {
        /// <inheritdoc />
        /// Moves two BetaBooks comments from "Chapter 30 - Summons" to "Chapter 3 - Under Pressure".
        /// The importer's StartsWith("Chapter 3") match hit "Chapter 30..." before the correct target.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Comments""
                SET    ""SectionId"" = '82a9ae2f-5f9c-48ef-9c46-477a7a19bd5c'
                WHERE  ""SectionId"" = '08bbb476-98f3-4ac0-85a3-d7b469e7188a'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Comments""
                SET    ""SectionId"" = '08bbb476-98f3-4ac0-85a3-d7b469e7188a'
                WHERE  ""SectionId"" = '82a9ae2f-5f9c-48ef-9c46-477a7a19bd5c'
                  AND  ""CreatedAt""  < '2026-05-01'
            ");
        }
    }
}
