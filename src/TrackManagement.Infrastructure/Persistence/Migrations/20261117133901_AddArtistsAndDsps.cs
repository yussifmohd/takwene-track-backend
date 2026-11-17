using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TrackManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistsAndDsps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dsps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dsps", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "dsps",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { new Guid("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0001"), "Spotify" },
                    { new Guid("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0002"), "Apple Music" },
                    { new Guid("0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0003"), "YouTube Music" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_artists_normalized_email",
                table: "artists",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dsps_name",
                table: "dsps",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "dsps");
        }
    }
}
