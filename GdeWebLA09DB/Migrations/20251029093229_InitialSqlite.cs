using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GdeWebLA09DB.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "T_ROLE",
                columns: table => new
                {
                    ROLEID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ROLENAME = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VALID = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MODIFIER = table.Column<int>(type: "INTEGER", nullable: false),
                    MODIFICATIONDATE = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_ROLE", x => x.ROLEID);
                });

            migrationBuilder.CreateTable(
                name: "T_USER",
                columns: table => new
                {
                    USERID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GUID = table.Column<string>(type: "TEXT", nullable: false),
                    PASSWORD = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FIRSTNAME = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LASTNAME = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EMAIL = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ACTIVE = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MODIFICATIONDATE = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_USER", x => x.USERID);
                });

            migrationBuilder.CreateTable(
                name: "K_USER_ROLES",
                columns: table => new
                {
                    USERROLES = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    USERID = table.Column<int>(type: "INTEGER", nullable: false),
                    ROLEID = table.Column<int>(type: "INTEGER", nullable: false),
                    CREATOR = table.Column<int>(type: "INTEGER", nullable: false),
                    CREATINGDATE = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_K_USER_ROLES", x => x.USERROLES);
                    table.ForeignKey(
                        name: "FK_K_USER_ROLES_T_ROLE_ROLEID",
                        column: x => x.ROLEID,
                        principalTable: "T_ROLE",
                        principalColumn: "ROLEID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_K_USER_ROLES_T_USER_USERID",
                        column: x => x.USERID,
                        principalTable: "T_USER",
                        principalColumn: "USERID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "T_AUTHENTICATION",
                columns: table => new
                {
                    TOKENID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    USERID = table.Column<int>(type: "INTEGER", nullable: false),
                    TOKEN = table.Column<string>(type: "TEXT", nullable: false),
                    EXPIRATIONDATE = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_AUTHENTICATION", x => x.TOKENID);
                    table.ForeignKey(
                        name: "FK_T_AUTHENTICATION_T_USER_USERID",
                        column: x => x.USERID,
                        principalTable: "T_USER",
                        principalColumn: "USERID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "T_ROLE",
                columns: new[] { "ROLEID", "MODIFICATIONDATE", "MODIFIER", "ROLENAME", "VALID" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Admin", true },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "User", true }
                });

            migrationBuilder.InsertData(
                table: "T_USER",
                columns: new[] { "USERID", "ACTIVE", "EMAIL", "FIRSTNAME", "GUID", "LASTNAME", "MODIFICATIONDATE", "PASSWORD" },
                values: new object[] { 1, true, "jakab.d@gmail.com", "Dávid", "0d2f1a91-ba24-4203-9b89-2d7f19ac9a7a", "Jakab", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3774b62586f7e44b343a0f2cc8e0e336c0d03549e09162cb73dfcb9a72c15a95c40f5a5b1608a08a28a79f46450ad3391f0a9342a19fdebb56511b556ba6aabb" });

            migrationBuilder.InsertData(
                table: "K_USER_ROLES",
                columns: new[] { "USERROLES", "CREATINGDATE", "CREATOR", "ROLEID", "USERID" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 1, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_K_USER_ROLES_ROLEID",
                table: "K_USER_ROLES",
                column: "ROLEID");

            migrationBuilder.CreateIndex(
                name: "IX_K_USER_ROLES_USERID",
                table: "K_USER_ROLES",
                column: "USERID");

            migrationBuilder.CreateIndex(
                name: "IX_T_AUTHENTICATION_USERID",
                table: "T_AUTHENTICATION",
                column: "USERID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "K_USER_ROLES");

            migrationBuilder.DropTable(
                name: "T_AUTHENTICATION");

            migrationBuilder.DropTable(
                name: "T_ROLE");

            migrationBuilder.DropTable(
                name: "T_USER");
        }
    }
}
