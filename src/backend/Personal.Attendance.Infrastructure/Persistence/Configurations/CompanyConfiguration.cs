using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Personal.Attendance.Domain.Entities;

namespace Personal.Attendance.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("ATTENDANCE_COMPANIES");
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).HasColumnName("CompanyId");
        builder.Property(company => company.CompanyCode).HasMaxLength(50).IsRequired();
        builder.Property(company => company.NormalizedCompanyCode).HasMaxLength(50).IsRequired();
        builder.Property(company => company.Name).HasMaxLength(200).IsRequired();
        builder.Property(company => company.IsActive).IsRequired();
        builder.Property(company => company.NextEmployeeNumber).IsRequired();
        builder.Property(company => company.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.HasIndex(company => company.NormalizedCompanyCode).IsUnique();
    }
}
