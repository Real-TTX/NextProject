using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalendarToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CalendarToken",
                table: "User",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // every existing user gets a token of their own (the unique index needs them to differ)
            migrationBuilder.Sql("UPDATE \"User\" SET \"CalendarToken\" = gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_User_CalendarToken",
                table: "User",
                column: "CalendarToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_CalendarToken",
                table: "User");

            migrationBuilder.DropColumn(
                name: "CalendarToken",
                table: "User");
        }
    }
}
