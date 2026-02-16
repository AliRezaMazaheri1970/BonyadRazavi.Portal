using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonyadRazavi.Auth.Infrastructure.Persistence.Configurations;

public sealed class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> builder)
    {
        builder.ToTable("UserCompanies");

        builder.HasKey(userCompany => userCompany.UserId);

        builder.Property(userCompany => userCompany.UserId)
            .HasColumnName("UserId")
            .ValueGeneratedNever();

        builder.Property(userCompany => userCompany.CompanyCode)
            .IsRequired();

        builder.Property(userCompany => userCompany.CompanyName)
            .HasMaxLength(300);

        builder.Property(userCompany => userCompany.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(userCompany => userCompany.CreatedAtUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(userCompany => userCompany.CompanyCode);
    }
}
