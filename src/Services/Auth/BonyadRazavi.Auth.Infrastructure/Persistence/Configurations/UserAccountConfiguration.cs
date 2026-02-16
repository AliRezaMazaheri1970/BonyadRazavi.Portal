using System.Text.Json;
using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BonyadRazavi.Auth.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasColumnName("UserId")
            .ValueGeneratedNever();

        builder.Property(user => user.UserName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(user => user.UserName)
            .IsUnique();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(user => user.UpdatedAtUtc)
            .HasColumnType("datetime2");

        var rolesConverter = new ValueConverter<List<string>, string>(
            roles => JsonSerializer.Serialize(roles, (JsonSerializerOptions?)null),
            json => string.IsNullOrWhiteSpace(json)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());
        var rolesComparer = new ValueComparer<List<string>>(
            (left, right) =>
                left == right || (left != null && right != null && left.SequenceEqual(right)),
            roles =>
                roles == null
                    ? 0
                    : roles.Aggregate(0, (current, role) => HashCode.Combine(current, role != null ? role.GetHashCode() : 0)),
            roles => roles == null ? new List<string>() : roles.ToList());

        var rolesProperty = builder.Property(user => user.Roles)
            .HasConversion(rolesConverter)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        rolesProperty.Metadata.SetValueComparer(rolesComparer);

        builder.HasOne(user => user.Company)
            .WithOne(company => company.User)
            .HasForeignKey<UserCompany>(company => company.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.ActionLogs)
            .WithOne(log => log.User)
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
