using CodeJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeJudge.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Submission"/> per Database Design §6 and §10.</summary>
public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions", t =>
        {
            t.HasCheckConstraint(
                "ck_submissions_language",
                "language IN ('CSharp','Python','JavaScript')");
            t.HasCheckConstraint(
                "ck_submissions_status",
                "status IN ('Pending','Evaluating','Completed','Error')");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.ProblemId).IsRequired().HasMaxLength(100);

        builder.Property(s => s.Language).IsRequired().HasConversion<string>();
        builder.Property(s => s.Code).IsRequired().HasColumnType("text");

        builder.Property(s => s.Status).IsRequired().HasConversion<string>();

        builder.Property(s => s.LockedBy).HasMaxLength(100);
        builder.Property(s => s.LockedUntil).HasColumnType("timestamptz");

        builder.Property(s => s.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.ErrorMessage).HasMaxLength(1000);

        builder.Property(s => s.CreatedAt).IsRequired().HasColumnType("timestamptz");
        builder.Property(s => s.StartedAt).HasColumnType("timestamptz");
        builder.Property(s => s.CompletedAt).HasColumnType("timestamptz");

        builder.HasMany(s => s.Results)
            .WithOne()
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Submission.Results))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Partial index over the queue columns — the hottest query in the system (Database Design §10).
        builder.HasIndex(s => new { s.Status, s.LockedUntil, s.CreatedAt })
            .HasDatabaseName("ix_submissions_queue")
            .HasFilter("status IN ('Pending','Evaluating')");

        // Covering index for the per-user history list; created_at descending for paging order.
        builder.HasIndex(s => new { s.UserId, s.CreatedAt })
            .HasDatabaseName("ix_submissions_user_id_created_at")
            .IsDescending(false, true)
            .IncludeProperties(s => new { s.ProblemId, s.Language, s.Status });
    }
}
