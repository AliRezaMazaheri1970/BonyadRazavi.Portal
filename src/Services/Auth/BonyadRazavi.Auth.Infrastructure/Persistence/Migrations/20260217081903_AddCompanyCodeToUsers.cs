using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BonyadRazavi.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCodeToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyCode",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE u
                SET u.CompanyCode = uc.CompanyCode
                FROM dbo.Users u
                INNER JOIN dbo.UserCompanies uc ON uc.UserId = u.UserId
                WHERE u.CompanyCode = '00000000-0000-0000-0000-000000000000'
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyCode",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyCode",
                table: "Users",
                column: "CompanyCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyCode",
                table: "Users");

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyCode",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.DropColumn(
                name: "CompanyCode",
                table: "Users");
        }
    }
}
