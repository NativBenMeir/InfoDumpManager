using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoDumpManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotTextToGEM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotText",
                table: "Gems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotText",
                table: "Gems");
        }
    }
}
