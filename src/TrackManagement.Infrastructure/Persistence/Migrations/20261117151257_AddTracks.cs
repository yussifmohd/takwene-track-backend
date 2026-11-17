using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    isrc = table.Column<string>(type: "character(12)", fixedLength: true, maxLength: 12, nullable: false),
                    genre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracks", x => x.id);
                    table.CheckConstraint("ck_tracks_genre", "genre IN ('Arabic Pop', 'Mahraganat', 'Khaleeji', 'Hip-Hop', 'Electronic', 'Rap', 'R&B', 'Rock')");
                    table.CheckConstraint("ck_tracks_isrc_format", "isrc ~ '^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$'");
                    table.CheckConstraint("ck_tracks_status", "status IN ('draft', 'submitted', 'distributed')");
                    table.ForeignKey(
                        name: "fk_tracks_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_artist_id",
                table: "tracks",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_isrc",
                table: "tracks",
                column: "isrc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracks_release_date_id",
                table: "tracks",
                columns: new[] { "release_date", "id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_status",
                table: "tracks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tracks");
        }
    }
}
