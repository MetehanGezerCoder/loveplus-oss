using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace LovePlus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealDeviceTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Mood",
                table: "user_live_status_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "user_live_status_snapshots",
                type: "geography (point)",
                nullable: true,
                oldClrType: typeof(Point),
                oldType: "geography (point)");

            migrationBuilder.AlterColumn<double>(
                name: "Accuracy",
                table: "user_live_status_snapshots",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<double>(
                name: "Altitude",
                table: "user_live_status_snapshots",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatteryState",
                table: "user_live_status_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<double>(
                name: "Heading",
                table: "user_live_status_snapshots",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocationSharingEnabled",
                table: "user_live_status_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationRecordedAtUtc",
                table: "user_live_status_snapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShareLastKnownLocation",
                table: "user_live_status_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "user_live_status_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RealDevice");

            migrationBuilder.AddColumn<double>(
                name: "Speed",
                table: "user_live_status_snapshots",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Altitude",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "BatteryState",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "Heading",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "IsLocationSharingEnabled",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "LocationRecordedAtUtc",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "ShareLastKnownLocation",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "user_live_status_snapshots");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "user_live_status_snapshots");

            migrationBuilder.AlterColumn<string>(
                name: "Mood",
                table: "user_live_status_snapshots",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "user_live_status_snapshots",
                type: "geography (point)",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography (point)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Accuracy",
                table: "user_live_status_snapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
