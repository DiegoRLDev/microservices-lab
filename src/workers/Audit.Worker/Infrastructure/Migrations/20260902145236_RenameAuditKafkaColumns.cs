using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroservicesLab.AuditWorker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAuditKafkaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "partition",
                table: "audit_events",
                newName: "kafka_partition");

            migrationBuilder.RenameColumn(
                name: "offset",
                table: "audit_events",
                newName: "kafka_offset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "kafka_partition",
                table: "audit_events",
                newName: "partition");

            migrationBuilder.RenameColumn(
                name: "kafka_offset",
                table: "audit_events",
                newName: "offset");
        }
    }
}
