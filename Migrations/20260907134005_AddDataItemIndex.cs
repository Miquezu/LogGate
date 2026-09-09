using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogGate.Migrations
{
    /// <inheritdoc />
    public partial class AddDataItemIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataItems_RecordNumber_EventTime",
                table: "DataItems",
                columns: new[] { "RecordNumber", "EventTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataItems_RecordNumber_EventTime",
                table: "DataItems");
        }
    }
}
