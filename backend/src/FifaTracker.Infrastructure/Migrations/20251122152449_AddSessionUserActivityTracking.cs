using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FifaTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionUserActivityTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActiveInSession",
                table: "SessionUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResumedAt",
                table: "SessionUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "SessionUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TotalActiveTime",
                table: "SessionUsers",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActiveInSession",
                table: "SessionUsers");

            migrationBuilder.DropColumn(
                name: "LastResumedAt",
                table: "SessionUsers");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "SessionUsers");

            migrationBuilder.DropColumn(
                name: "TotalActiveTime",
                table: "SessionUsers");
        }
    }
}
