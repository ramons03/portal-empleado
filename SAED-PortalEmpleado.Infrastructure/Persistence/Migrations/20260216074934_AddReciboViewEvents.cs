using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAED_PortalEmpleado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReciboViewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReciboViewEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GoogleSub = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Cuil = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ReciboId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ViewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReciboViewEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReciboViewEvents_Cuil",
                table: "ReciboViewEvents",
                column: "Cuil");

            migrationBuilder.CreateIndex(
                name: "IX_ReciboViewEvents_GoogleSub",
                table: "ReciboViewEvents",
                column: "GoogleSub");

            migrationBuilder.CreateIndex(
                name: "IX_ReciboViewEvents_ViewedAtUtc",
                table: "ReciboViewEvents",
                column: "ViewedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReciboViewEvents");
        }
    }
}
