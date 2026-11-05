using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Floof_ConsentToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_settings");

            migrationBuilder.CreateTable(
                name: "consent_toggle",
                columns: table => new
                {
                    consent_toggle_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    toggle_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_toggle", x => x.consent_toggle_id);
                    table.ForeignKey(
                        name: "FK_consent_toggle_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_toggle_profile_id_toggle_id",
                table: "consent_toggle",
                columns: new[] { "profile_id", "toggle_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_toggle");

            migrationBuilder.CreateTable(
                name: "consent_settings",
                columns: table => new
                {
                    consent_settings_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    consent_freetext = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_settings", x => x.consent_settings_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_settings_user_id",
                table: "consent_settings",
                column: "user_id",
                unique: true);
        }
    }
}
