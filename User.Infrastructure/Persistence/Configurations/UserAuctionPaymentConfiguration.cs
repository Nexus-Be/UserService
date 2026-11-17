using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

namespace User.Infrastructure.Persistence.Configurations;

public sealed class UserAuctionPaymentConfiguration : IEntityTypeConfiguration<UserAuctionPayment>
{
    public void Configure(EntityTypeBuilder<UserAuctionPayment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
