using AuthSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthSystem.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .IsRequired();

        builder.Property(r => r.CreatedOn)
            .IsRequired();

        builder.Property(r => r.ExpiresOn)
            .IsRequired();

        builder.Property(r => r.RevokedOn)
            .IsRequired(false);

        builder.HasOne(r => r.ApplicationUser)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(r => r.IsExpired);
        builder.Ignore(r => r.IsActive);
    }
}