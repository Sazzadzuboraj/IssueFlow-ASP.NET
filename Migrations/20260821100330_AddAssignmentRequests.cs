using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssueFlow.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GatewayResponse",
                table: "PaymentTransactions",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AssignmentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManagerResponse = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlternativeTargetType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AlternativeTargetId = table.Column<int>(type: "int", nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRequests_AspNetUsers_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentRequests_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRequests_RequesterId",
                table: "AssignmentRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRequests_ReviewedById",
                table: "AssignmentRequests",
                column: "ReviewedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentRequests");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayResponse",
                table: "PaymentTransactions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true);
        }
    }
}
