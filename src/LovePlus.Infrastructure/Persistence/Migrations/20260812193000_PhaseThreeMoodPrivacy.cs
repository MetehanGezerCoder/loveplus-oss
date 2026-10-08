using LovePlus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LovePlus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LovePlusDbContext))]
[Migration("20260812193000_PhaseThreeMoodPrivacy")]
public sealed class PhaseThreeMoodPrivacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsMoodSharingEnabled",
            table: "user_live_status_snapshots",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsMoodSharingEnabled",
            table: "user_live_status_snapshots");
    }
}
