using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicJobRunnerApp.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CronExpression = table.Column<string>(type: "text", nullable: false),
                    Script = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnqueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobSchedules_JobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "JobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSchedules_JobDefinitionId",
                table: "JobSchedules",
                column: "JobDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSchedules");

            migrationBuilder.DropTable(
                name: "JobDefinitions");
        }
    }
}
