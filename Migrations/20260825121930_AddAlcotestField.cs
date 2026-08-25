using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogGate.Migrations
{
    /// <inheritdoc />
    public partial class AddAlcotestField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresAlcotest",
                table: "WorkScheduleRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresAlcotest",
                table: "WorkScheduleRules");
        }
    }
}
