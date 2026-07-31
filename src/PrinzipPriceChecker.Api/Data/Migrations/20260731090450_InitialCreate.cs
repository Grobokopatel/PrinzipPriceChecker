using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrinzipPriceChecker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubscriptionId = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackedFlatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    FlatUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OldPrice = table.Column<long>(type: "INTEGER", nullable: true),
                    NewPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedFlats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CurrentPrice = table.Column<long>(type: "INTEGER", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastPriceChangeAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastCheckError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedFlats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackedFlatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<long>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceHistory_TrackedFlats_TrackedFlatId",
                        column: x => x.TrackedFlatId,
                        principalTable: "TrackedFlats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false, collation: "NOCASE"),
                    TrackedFlatId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_TrackedFlats_TrackedFlatId",
                        column: x => x.TrackedFlatId,
                        principalTable: "TrackedFlats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistory_TrackedFlatId_DetectedAt",
                table: "PriceHistory",
                columns: new[] { "TrackedFlatId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TrackedFlatId_Email",
                table: "Subscriptions",
                columns: new[] { "TrackedFlatId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFlats_Url",
                table: "TrackedFlats",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PriceHistory");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TrackedFlats");
        }
    }
}
