using CustomerManagement.Api.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Infrastructure.Persistence;

public sealed class CustomerManagementDbContext(DbContextOptions<CustomerManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ContactDetail> ContactDetails => Set<ContactDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(c => c.Company)
                .HasMaxLength(200);
            entity.Property(c => c.CreatedAtUtc)
                .IsRequired();
            entity.Property(c => c.UpdatedAtUtc)
                .IsRequired();

            entity.HasMany(c => c.ContactDetails)
                .WithOne(cd => cd.Customer)
                .HasForeignKey(cd => cd.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactDetail>(entity =>
        {
            entity.ToTable("ContactDetails");
            entity.HasKey(cd => cd.Id);
            entity.Property(cd => cd.Channel)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(cd => cd.Value)
                .HasMaxLength(320)
                .IsRequired();
            entity.Property(cd => cd.Label)
                .HasMaxLength(100);
            entity.Property(cd => cd.IsPrimary)
                .IsRequired();
            entity.Property(cd => cd.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(cd => new { cd.CustomerId, cd.Channel });
        });
    }
}
