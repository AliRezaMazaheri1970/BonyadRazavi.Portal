using BonyadRazavi.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonyadRazavi.Auth.Infrastructure.Persistence.Configurations;

public sealed class UserActionLogConfiguration : IEntityTypeConfiguration<UserActionLog>
{
    public void Configure(EntityTypeBuilder<UserActionLog> builder)
    {
        builder.ToTable(
            "UserActionLogs",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_UserActionLogs_Metadata_IsJson",
                "ISJSON([Metadata]) > 0"));

        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id)
            .HasColumnName("UserActionLogId")
            .ValueGeneratedOnAdd();

        builder.Property(log => log.UserId);

        builder.Property(log => log.ActionDateUtc)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(log => log.ActionType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(log => log.Metadata)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.HasIndex(log => log.ActionDateUtc);
        builder.HasIndex(log => new { log.UserId, log.ActionDateUtc });
        builder.HasIndex(log => log.ActionType);
    }
}
