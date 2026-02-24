using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAED_PortalEmpleado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Cuit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AddressLine = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Province = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyProfiles");
        }
    }
}
