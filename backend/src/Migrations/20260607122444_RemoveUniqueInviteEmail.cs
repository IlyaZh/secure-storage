using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureStorage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueInviteEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invites_Email",
                table: "invites");

            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "secrets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_invites_Email",
                table: "invites",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invites_Email",
                table: "invites");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "secrets");

            migrationBuilder.CreateIndex(
                name: "IX_invites_Email",
                table: "invites",
                column: "Email",
                unique: true);
        }
    }
}
