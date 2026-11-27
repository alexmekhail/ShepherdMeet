using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinimalApiProject.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261127180000_ProtectAppointmentSlots")]
public sealed class ProtectAppointmentSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("AvailabilityID", "Appointments", nullable: true);
        migrationBuilder.AddColumn<DateTime>("StartUtc", "Appointments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>("EndUtc", "Appointments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex("IX_Appointments_AvailabilityID", "Appointments", "AvailabilityID", unique: true);
        migrationBuilder.AddForeignKey("FK_Appointments_PriestAvailabilities_AvailabilityID", "Appointments",
            "AvailabilityID", "PriestAvailabilities", principalColumn: "ID", onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            ALTER TABLE "Appointments"
                ADD CONSTRAINT "CK_Appointments_Interval"
                    CHECK (("AvailabilityID" IS NULL AND "StartUtc" IS NULL AND "EndUtc" IS NULL)
                        OR ("AvailabilityID" IS NOT NULL AND "StartUtc" IS NOT NULL AND "EndUtc" IS NOT NULL
                            AND "EndUtc" - "StartUtc" = interval '30 minutes')),
                ADD CONSTRAINT "EX_Appointments_Interval" EXCLUDE USING gist
                    (tstzrange("StartUtc", "EndUtc", '[)') WITH &&) WHERE ("StartUtc" IS NOT NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Appointments" DROP CONSTRAINT "EX_Appointments_Interval",
                DROP CONSTRAINT "CK_Appointments_Interval";
            """);
        migrationBuilder.DropForeignKey("FK_Appointments_PriestAvailabilities_AvailabilityID", "Appointments");
        migrationBuilder.DropIndex("IX_Appointments_AvailabilityID", "Appointments");
        migrationBuilder.DropColumn("AvailabilityID", "Appointments");
        migrationBuilder.DropColumn("StartUtc", "Appointments");
        migrationBuilder.DropColumn("EndUtc", "Appointments");
    }
}
