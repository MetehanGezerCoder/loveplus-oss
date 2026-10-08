using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LovePlus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DevicePushTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_push_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_push_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_push_tokens_device_sessions_DeviceSessionId",
                        column: x => x.DeviceSessionId,
                        principalTable: "device_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_push_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_push_tokens_DeviceSessionId",
                table: "device_push_tokens",
                column: "DeviceSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_push_tokens_Token",
                table: "device_push_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_push_tokens_UserId_DisabledAtUtc",
                table: "device_push_tokens",
                columns: new[] { "UserId", "DisabledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_push_tokens");
        }
    }
}
