using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKeepingWeb.Migrations
{
    /// <inheritdoc />
    public partial class MakeSlugsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "UploadContents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "UploadContents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadContents_Slug",
                table: "UploadContents",
                column: "Slug",
                unique: true);
        }
    }
}
