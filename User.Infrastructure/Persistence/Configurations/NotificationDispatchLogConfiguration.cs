namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class NotificationDispatchLogConfiguration : IEntityTypeConfiguration<NotificationDispatchLog>
{
    public void Configure(EntityTypeBuilder<NotificationDispatchLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceEventId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(128);
        builder.Property(x => x.Endpoint).HasMaxLength(250).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.NotificationId).HasMaxLength(150);

        builder.HasIndex(x => x.SourceEventId);
        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
