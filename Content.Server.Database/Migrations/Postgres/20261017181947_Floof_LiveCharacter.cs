using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Floof_LiveCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_character",
                columns: table => new
                {
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_saved_round = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_character", x => x.profile_id);
                    table.ForeignKey(
                        name: "FK_live_character_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_character_history",
                columns: table => new
                {
                    live_character_history_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saved_round = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_character_history", x => x.live_character_history_id);
                    table.ForeignKey(
                        name: "FK_live_character_history_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_live_character_history_profile_id_saved_at",
                table: "live_character_history",
                columns: new[] { "profile_id", "saved_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_character");

            migrationBuilder.DropTable(
                name: "live_character_history");
        }
    }
}
