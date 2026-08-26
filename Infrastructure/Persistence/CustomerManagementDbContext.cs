using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Infrastructure.Persistence;

public sealed class CustomerManagementDbContext(DbContextOptions<CustomerManagementDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ContactDetail> ContactDetails => Set<ContactDetail>();

    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    public DbSet<CustomerInteractionEvent> CustomerInteractionEvents => Set<CustomerInteractionEvent>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.DisplayName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(u => u.IsActive)
                .IsRequired();
            entity.Property(u => u.CreatedAtUtc)
                .IsRequired();
            entity.Property(u => u.UpdatedAtUtc)
                .IsRequired();
            entity.Property(u => u.DeactivatedAtUtc);
            entity.Property(u => u.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.ToTable("Roles");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.TokenHash)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(r => r.ExpiresAtUtc)
                .IsRequired();
            entity.Property(r => r.CreatedAtUtc)
                .IsRequired();
            entity.Property(r => r.ReplacedByTokenHash)
                .HasMaxLength(512);
            entity.Property(r => r.CreatedByIp)
                .HasMaxLength(64);

            entity.HasIndex(r => r.TokenHash)
                .IsUnique();
            entity.HasIndex(r => new { r.UserId, r.ExpiresAtUtc });

            entity.HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(p => p.Description)
                .HasMaxLength(500);
            entity.Property(p => p.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(p => p.Name)
                .IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            entity.Property(rp => rp.CreatedAtUtc)
                .IsRequired();

            entity.HasOne<IdentityRole<Guid>>()
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rp => rp.PermissionId);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.OccurredAtUtc)
                .IsRequired();
            entity.Property(a => a.ActorEmail)
                .HasMaxLength(320);
            entity.Property(a => a.ActionType)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(a => a.EntityName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(a => a.EntityId)
                .HasMaxLength(128)
                .IsRequired();
            entity.Property(a => a.Result)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(a => a.MetadataJson)
                .HasMaxLength(4000);

            entity.HasIndex(a => a.OccurredAtUtc)
                .IsDescending();
            entity.HasIndex(a => new { a.ActorUserId, a.OccurredAtUtc });
            entity.HasIndex(a => new { a.ActionType, a.OccurredAtUtc });
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Key)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(setting => setting.Value)
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(setting => setting.Description)
                .HasMaxLength(500);
            entity.Property(setting => setting.UpdatedAtUtc)
                .IsRequired();
            entity.Property(setting => setting.RowVersion)
                .IsRowVersion();

            entity.HasIndex(setting => setting.Key)
                .IsUnique();
        });

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
