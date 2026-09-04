using CodeJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeJudge.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="EvaluationResult"/> per Database Design §6 and §10.</summary>
public sealed class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    public void Configure(EntityTypeBuilder<EvaluationResult> builder)
    {
        builder.ToTable("evaluation_results", t =>
        {
            t.HasCheckConstraint(
                "ck_evaluation_results_rubric_item",
                "rubric_item IN ('Security','Compiles','Test')");
            t.HasCheckConstraint(
                "ck_evaluation_results_tests_count",
                "tests_passed IS NULL OR tests_total IS NULL OR tests_passed <= tests_total");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.SubmissionId).IsRequired();

        builder.Property(r => r.RubricItem).IsRequired().HasConversion<string>();
        builder.Property(r => r.Passed).IsRequired();
        builder.Property(r => r.Skipped).IsRequired().HasDefaultValue(false);

        builder.Property(r => r.Message).HasMaxLength(2000);
        builder.Property(r => r.Output).HasColumnType("jsonb");

        builder.Property(r => r.TestsPassed);
        builder.Property(r => r.TestsTotal);
        builder.Property(r => r.DurationMs);

        builder.Property(r => r.EvaluatedAt).IsRequired().HasColumnType("timestamptz");

        // One result per rubric item; also the duplicate-writer guard (Database Design §5, §8).
        builder.HasIndex(r => new { r.SubmissionId, r.RubricItem })
            .IsUnique()
            .HasDatabaseName("ux_evaluation_results_submission_rubric");
    }
}
