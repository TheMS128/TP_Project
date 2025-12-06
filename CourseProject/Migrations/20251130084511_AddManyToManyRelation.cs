using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseProject.Migrations
{
    /// <inheritdoc />
    public partial class AddManyToManyRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubjectUser_AspNetUsers_TeachersId",
                table: "SubjectUser");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectUser_Subjects_AssignedSubjectsId",
                table: "SubjectUser");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubjectUser",
                table: "SubjectUser");

            migrationBuilder.RenameTable(
                name: "SubjectUser",
                newName: "SubjectsUsers");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectUser_TeachersId",
                table: "SubjectsUsers",
                newName: "IX_SubjectsUsers_TeachersId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubjectsUsers",
                table: "SubjectsUsers",
                columns: new[] { "AssignedSubjectsId", "TeachersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectsUsers_AspNetUsers_TeachersId",
                table: "SubjectsUsers",
                column: "TeachersId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectsUsers_Subjects_AssignedSubjectsId",
                table: "SubjectsUsers",
                column: "AssignedSubjectsId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubjectsUsers_AspNetUsers_TeachersId",
                table: "SubjectsUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_SubjectsUsers_Subjects_AssignedSubjectsId",
                table: "SubjectsUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubjectsUsers",
                table: "SubjectsUsers");

            migrationBuilder.RenameTable(
                name: "SubjectsUsers",
                newName: "SubjectUser");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectsUsers_TeachersId",
                table: "SubjectUser",
                newName: "IX_SubjectUser_TeachersId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubjectUser",
                table: "SubjectUser",
                columns: new[] { "AssignedSubjectsId", "TeachersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectUser_AspNetUsers_TeachersId",
                table: "SubjectUser",
                column: "TeachersId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubjectUser_Subjects_AssignedSubjectsId",
                table: "SubjectUser",
                column: "AssignedSubjectsId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
