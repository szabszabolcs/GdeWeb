using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GdeWebDB.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleOAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OAUTHPROVIDER",
                table: "T_USER",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OAUTHID",
                table: "T_USER",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PROFILEPICTURE",
                table: "T_USER",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ONBOARDINGCOMPLETED",
                table: "T_USER",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OAUTHPROVIDER",
                table: "T_USER");

            migrationBuilder.DropColumn(
                name: "OAUTHID",
                table: "T_USER");

            migrationBuilder.DropColumn(
                name: "PROFILEPICTURE",
                table: "T_USER");

            migrationBuilder.DropColumn(
                name: "ONBOARDINGCOMPLETED",
                table: "T_USER");
        }
    }
}

