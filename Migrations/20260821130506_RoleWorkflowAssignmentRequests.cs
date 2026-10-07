using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssueFlow.Migrations
{
    /// <inheritdoc />
    public partial class RoleWorkflowAssignmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfferedToUserId",
                table: "AssignmentRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestKind",
                table: "AssignmentRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRequests_OfferedToUserId",
                table: "AssignmentRequests",
                column: "OfferedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentRequests_AspNetUsers_OfferedToUserId",
                table: "AssignmentRequests",
                column: "OfferedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentRequests_AspNetUsers_OfferedToUserId",
                table: "AssignmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentRequests_OfferedToUserId",
                table: "AssignmentRequests");

            migrationBuilder.DropColumn(
                name: "OfferedToUserId",
                table: "AssignmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestKind",
                table: "AssignmentRequests");
        }
    }
}
