using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.BrowserReports.Domain;

namespace IHFiction.Data.BrowserReports.Configurations;

internal sealed class CspViolationReportRecordConfiguration : IEntityTypeConfiguration<CspViolationReportRecord>
{
    public void Configure(EntityTypeBuilder<CspViolationReportRecord> builder)
    {
        builder.ToTable("csp_violation_reports");

        builder.Property(report => report.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64);

        builder.Property(report => report.EffectiveDirective)
            .HasColumnName("effective_directive")
            .HasMaxLength(200);

        builder.Property(report => report.BlockedResource)
            .HasColumnName("blocked_url")
            .HasMaxLength(2000);

        builder.Property(report => report.DocumentResource)
            .HasColumnName("document_url")
            .HasMaxLength(2000);

        builder.Property(report => report.SourceFile)
            .HasColumnName("source_file")
            .HasMaxLength(2000);

        builder.Property(report => report.StatusCode)
            .HasColumnName("status_code");

        builder.Property(report => report.Disposition)
            .HasColumnName("disposition")
            .HasMaxLength(50);

        builder.Property(report => report.OriginalPolicy)
            .HasColumnName("original_policy")
            .HasMaxLength(4000);

        builder.Property(report => report.FirstSeenAt)
            .HasColumnName("first_seen_at");

        builder.Property(report => report.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(report => report.OccurrenceCount)
            .HasColumnName("occurrence_count");

        builder.Property(report => report.LastUserAgent)
            .HasColumnName("last_user_agent")
            .HasMaxLength(1000);

        builder.Property(report => report.LastSample)
            .HasColumnName("last_sample")
            .HasMaxLength(2000);

        builder.Property(report => report.LastLineNumber)
            .HasColumnName("last_line_number");

        builder.Property(report => report.LastColumnNumber)
            .HasColumnName("last_column_number");

        builder.HasIndex(report => report.Fingerprint)
            .IsUnique();
    }
}

internal sealed class BrowserReportPayloadRecordConfiguration : IEntityTypeConfiguration<BrowserReportPayloadRecord>
{
    public void Configure(EntityTypeBuilder<BrowserReportPayloadRecord> builder)
    {
        builder.ToTable("browser_report_payloads");

        builder.Property(report => report.ReportType)
            .HasColumnName("report_type")
            .HasMaxLength(200);

        builder.Property(report => report.PayloadHash)
            .HasColumnName("payload_hash")
            .HasMaxLength(64);

        builder.Property(report => report.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb");

        builder.Property(report => report.ReportResource)
            .HasColumnName("report_url")
            .HasMaxLength(2000);

        builder.Property(report => report.SummaryKey)
            .HasColumnName("summary_key")
            .HasMaxLength(500);

        builder.Property(report => report.SummaryMessage)
            .HasColumnName("summary_message")
            .HasMaxLength(2000);

        builder.Property(report => report.BlockedResource)
            .HasColumnName("blocked_resource")
            .HasMaxLength(2000);

        builder.Property(report => report.SourceFile)
            .HasColumnName("source_file")
            .HasMaxLength(2000);

        builder.Property(report => report.LineNumber)
            .HasColumnName("line_number");

        builder.Property(report => report.ColumnNumber)
            .HasColumnName("column_number");

        builder.Property(report => report.Disposition)
            .HasColumnName("disposition")
            .HasMaxLength(50);

        builder.Property(report => report.FirstSeenAt)
            .HasColumnName("first_seen_at");

        builder.Property(report => report.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(report => report.OccurrenceCount)
            .HasColumnName("occurrence_count");

        builder.Property(report => report.LastUserAgent)
            .HasColumnName("last_user_agent")
            .HasMaxLength(1000);

        builder.HasIndex(report => new { report.ReportType, report.PayloadHash })
            .IsUnique();
    }
}
