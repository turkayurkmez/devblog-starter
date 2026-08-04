using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevBlog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingInMinutesToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReadingInMinutes",
                table: "Posts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Mevcut post'lar icin ReadingInMinutes'i 0 birakmak yaniltici olurdu (yayindaki
            // bir yazinin "0 dakika" surmesi anlamsiz); bunun yerine Content'teki kelime
            // sayisindan (bosluk sayisi + 1) 200 kelime/dakika varsayimiyla tahmini okuma
            // suresi hesaplaniyor, en az 1 dakika olacak sekilde.
            migrationBuilder.Sql(@"
                UPDATE Posts
                SET ReadingInMinutes = MAX(1, CAST(ROUND(
                    (LENGTH(Content) - LENGTH(REPLACE(Content, ' ', '')) + 1) / 200.0
                ) AS INTEGER));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadingInMinutes",
                table: "Posts");
        }
    }
}
