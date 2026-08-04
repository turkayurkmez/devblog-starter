using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevBlog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToPostSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Slug",
                table: "Posts");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Slug",
                table: "Posts",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Slug",
                table: "Posts");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Slug",
                table: "Posts",
                column: "Slug");
        }
    }
}
