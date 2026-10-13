using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrlShortener.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "click_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Referrer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeviceCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_click_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "urls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ApiKeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_urls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_click_events_ShortCode_OccurredAt",
                table: "click_events",
                columns: new[] { "ShortCode", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_urls_IdempotencyKey",
                table: "urls",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_urls_IsActive_ExpiresAt",
                table: "urls",
                columns: new[] { "IsActive", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_urls_ShortCode",
                table: "urls",
                column: "ShortCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "click_events");

            migrationBuilder.DropTable(
                name: "urls");
        }
    }
}
