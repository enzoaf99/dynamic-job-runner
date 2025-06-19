using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicJobRunnerApp.Migrations
{
    /// <inheritdoc />
    public partial class IsActiveJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "JobDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "JobDefinitions");
        }
    }
}
