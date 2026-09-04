using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    problem_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    locked_until = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_language", "language IN ('CSharp','Python','JavaScript')");
                    table.CheckConstraint("ck_submissions_status", "status IN ('Pending','Evaluating','Completed','Error')");
                });

            migrationBuilder.CreateTable(
                name: "evaluation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_item = table.Column<string>(type: "text", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    skipped = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    output = table.Column<string>(type: "jsonb", nullable: true),
                    tests_passed = table.Column<int>(type: "integer", nullable: true),
                    tests_total = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_results", x => x.id);
                    table.CheckConstraint("ck_evaluation_results_rubric_item", "rubric_item IN ('Security','Compiles','Test')");
                    table.CheckConstraint("ck_evaluation_results_tests_count", "tests_passed IS NULL OR tests_total IS NULL OR tests_passed <= tests_total");
                    table.ForeignKey(
                        name: "fk_evaluation_results_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_evaluation_results_submission_rubric",
                table: "evaluation_results",
                columns: new[] { "submission_id", "rubric_item" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_queue",
                table: "submissions",
                columns: new[] { "status", "locked_until", "created_at" },
                filter: "status IN ('Pending','Evaluating')");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_user_id_created_at",
                table: "submissions",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "problem_id", "language", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_results");

            migrationBuilder.DropTable(
                name: "submissions");
        }
    }
}
