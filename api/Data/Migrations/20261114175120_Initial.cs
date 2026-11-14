using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Console.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    tagline_ar = table.Column<string>(type: "text", nullable: false),
                    tagline_en = table.Column<string>(type: "text", nullable: false),
                    primary_color = table.Column<string>(type: "text", nullable: false),
                    accent_color = table.Column<string>(type: "text", nullable: false),
                    support_phone = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    android_application_id = table.Column<string>(type: "text", nullable: false),
                    ios_bundle_id = table.Column<string>(type: "text", nullable: false),
                    logo_svg = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    api_key_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinic_features",
                columns: table => new
                {
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinic_features", x => new { x.clinic_id, x.key });
                    table.ForeignKey(
                        name: "fk_clinic_features_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clinic_repos",
                columns: table => new
                {
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    html_url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinic_repos", x => new { x.clinic_id, x.kind });
                    table.ForeignKey(
                        name: "fk_clinic_repos_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    steps_json = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_jobs_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admins_email",
                table: "admins",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_at",
                table: "audit",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_clinic_id",
                table: "audit",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinics_slug",
                table: "clinics",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_clinic_id_created_at",
                table: "jobs",
                columns: new[] { "clinic_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admins");

            migrationBuilder.DropTable(
                name: "audit");

            migrationBuilder.DropTable(
                name: "clinic_features");

            migrationBuilder.DropTable(
                name: "clinic_repos");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "clinics");
        }
    }
}
