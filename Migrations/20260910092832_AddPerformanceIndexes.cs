using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogGate.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataItems_EventTime",
                table: "DataItems",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "IX_DataItems_FullName_EventTime",
                table: "DataItems",
                columns: new[] { "FullName", "EventTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataItems_EventTime",
                table: "DataItems");

            migrationBuilder.DropIndex(
                name: "IX_DataItems_FullName_EventTime",
                table: "DataItems");
        }
    }
}
