using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonyadRazavi.Auth.Infrastructure.Persistence.Configurations;

public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("RefreshTokenId")
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .IsRequired();

        builder.Property(token => token.TokenFamilyId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.CreatedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(token => token.CreatedByIp)
            .HasMaxLength(45);

        builder.Property(token => token.CreatedByUserAgent)
            .HasMaxLength(500);

        builder.Property(token => token.RevokedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(token => token.RevokedByIp)
            .HasMaxLength(45);

        builder.Property(token => token.RevocationReason)
            .HasMaxLength(200);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();
        builder.HasIndex(token => token.ExpiresAtUtc);
        builder.HasIndex(token => new { token.UserId, token.ExpiresAtUtc });
        builder.HasIndex(token => token.TokenFamilyId);
    }
}
