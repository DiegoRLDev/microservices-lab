using Microsoft.EntityFrameworkCore;
using MicroservicesLab.AuditWorker.Domain;

namespace MicroservicesLab.AuditWorker.Infrastructure;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");

            entity.HasKey(auditEvent => auditEvent.Id);

            entity.Property(auditEvent => auditEvent.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(auditEvent => auditEvent.EventId)
                .HasColumnName("event_id")
                .IsRequired();

            entity.Property(auditEvent => auditEvent.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(auditEvent => auditEvent.Topic)
                .HasColumnName("topic")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(auditEvent => auditEvent.MessageKey)
                .HasColumnName("message_key")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(auditEvent => auditEvent.Partition)
                .HasColumnName("kafka_partition")
                .IsRequired();

            entity.Property(auditEvent => auditEvent.Offset)
                .HasColumnName("kafka_offset")
                .IsRequired();

            entity.Property(auditEvent => auditEvent.OccurredAt)
                .HasColumnName("occurred_at");

            entity.Property(auditEvent => auditEvent.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(auditEvent => auditEvent.Payload)
                .HasColumnName("payload")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(auditEvent => auditEvent.AuditedAt)
                .HasColumnName("audited_at")
                .IsRequired();

            entity.HasIndex(auditEvent => auditEvent.EventId)
                .IsUnique()
                .HasDatabaseName("ux_audit_events_event_id");

            entity.HasIndex(auditEvent => auditEvent.EventType)
                .HasDatabaseName("ix_audit_events_event_type");

            entity.HasIndex(auditEvent => auditEvent.AuditedAt)
                .HasDatabaseName("ix_audit_events_audited_at");
        });
    }
}
