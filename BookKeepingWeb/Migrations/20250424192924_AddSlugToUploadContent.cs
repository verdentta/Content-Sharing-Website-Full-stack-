using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKeepingWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugToUploadContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "UploadContents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "UploadContents");
        }
    }
}
