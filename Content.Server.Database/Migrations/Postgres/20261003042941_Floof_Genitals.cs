using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Floof_Genitals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genitals",
                columns: table => new
                {
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    penis = table.Column<bool>(type: "boolean", nullable: false),
                    vagina = table.Column<bool>(type: "boolean", nullable: false),
                    breasts = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genitals", x => x.profile_id);
                    table.ForeignKey(
                        name: "FK_genitals_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });
            
            migrationBuilder.Sql("""
                INSERT INTO genitals (profile_id, penis, vagina, breasts)
                SELECT p.profile_id,
                    EXISTS (SELECT 1 FROM trait t WHERE t.profile_id = p.profile_id AND t.trait_name = 'CumProducer'),
                    EXISTS (SELECT 1 FROM trait t WHERE t.profile_id = p.profile_id AND t.trait_name = 'SquirtProducer'),
                    EXISTS (SELECT 1 FROM trait t WHERE t.profile_id = p.profile_id AND t.trait_name = 'MilkProducer')
                FROM (SELECT DISTINCT profile_id FROM trait WHERE trait_name IN ('CumProducer', 'SquirtProducer', 'MilkProducer')) p;
                """);

            migrationBuilder.Sql("DELETE FROM trait WHERE trait_name IN ('CumProducer', 'SquirtProducer', 'MilkProducer');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO trait (profile_id, trait_name) SELECT profile_id, 'CumProducer' FROM genitals WHERE penis;");
            migrationBuilder.Sql("INSERT INTO trait (profile_id, trait_name) SELECT profile_id, 'SquirtProducer' FROM genitals WHERE vagina;");
            migrationBuilder.Sql("INSERT INTO trait (profile_id, trait_name) SELECT profile_id, 'MilkProducer' FROM genitals WHERE breasts;");

            migrationBuilder.DropTable(
                name: "genitals");
        }
    }
}
