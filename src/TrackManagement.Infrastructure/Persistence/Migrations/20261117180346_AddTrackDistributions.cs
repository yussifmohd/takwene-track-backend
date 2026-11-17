using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackDistributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "track_distributions",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dsp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    answered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_distributions", x => new { x.track_id, x.dsp_id });
                    table.CheckConstraint("ck_track_distributions_answered", "(status = 'pending') = (answered_at_utc IS NULL)");
                    table.CheckConstraint("ck_track_distributions_status", "status IN ('pending', 'live', 'rejected')");
                    table.ForeignKey(
                        name: "fk_track_distributions_dsps_dsp_id",
                        column: x => x.dsp_id,
                        principalTable: "dsps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_track_distributions_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_track_distributions_dsp_id",
                table: "track_distributions",
                column: "dsp_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "track_distributions");
        }
    }
}
