using CustomerManagement.Api.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Infrastructure.Persistence;

public sealed class CustomerManagementDbContext(DbContextOptions<CustomerManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ContactDetail> ContactDetails => Set<ContactDetail>();

    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    public DbSet<CustomerInteractionEvent> CustomerInteractionEvents => Set<CustomerInteractionEvent>();

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
            entity.Property(c => c.RowVersion)
                .IsRowVersion();

            entity.HasMany(c => c.ContactDetails)
                .WithOne(cd => cd.Customer)
                .HasForeignKey(cd => cd.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Notes)
                .WithOne(n => n.Customer)
                .HasForeignKey(n => n.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Attachments)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.InteractionEvents)
                .WithOne(i => i.Customer)
                .HasForeignKey(i => i.CustomerId)
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

        modelBuilder.Entity<CustomerNote>(entity =>
        {
            entity.ToTable("CustomerNotes");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Body)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(n => n.CreatedBy)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(n => n.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(n => new { n.CustomerId, n.CreatedAtUtc });
        });

        modelBuilder.Entity<CustomerAttachment>(entity =>
        {
            entity.ToTable("CustomerAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.OriginalFileName)
                .HasMaxLength(260)
                .IsRequired();
            entity.Property(a => a.ContentType)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(a => a.SizeBytes)
                .IsRequired();
            entity.Property(a => a.StorageKey)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(a => a.CreatedBy)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(a => a.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(a => new { a.CustomerId, a.CreatedAtUtc });
            entity.HasIndex(a => a.StorageKey).IsUnique();
        });

        modelBuilder.Entity<CustomerInteractionEvent>(entity =>
        {
            entity.ToTable("CustomerInteractionEvents");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Channel)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(i => i.Direction)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(i => i.OccurredAtUtc)
                .IsRequired();
            entity.Property(i => i.Summary)
                .HasMaxLength(1000);
            entity.Property(i => i.SourceRef)
                .HasMaxLength(200);
            entity.Property(i => i.SourceSystem)
                .HasMaxLength(100);
            entity.Property(i => i.ProjectedAtUtc)
                .IsRequired();

            entity.HasIndex(i => new { i.CustomerId, i.OccurredAtUtc });
            entity.HasIndex(i => new { i.CustomerId, i.Channel, i.Direction, i.OccurredAtUtc });
        });
    }
}
