using CustomerManagement.Api.Domain.Customers;
using CustomerManagement.Api.Domain.Dashboard;
using CustomerManagement.Api.Domain.Faq;
using CustomerManagement.Api.Domain.Security;
using CustomerManagement.Api.Domain.Tickets;
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

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();

    public DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();

    public DbSet<TicketHistoryEntry> TicketHistoryEntries => Set<TicketHistoryEntry>();

    public DbSet<TicketTask> TicketTasks => Set<TicketTask>();

    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();

    public DbSet<TicketInternalNote> TicketInternalNotes => Set<TicketInternalNote>();

    public DbSet<TicketInternalNoteMention> TicketInternalNoteMentions => Set<TicketInternalNoteMention>();

    public DbSet<TicketHandoffRequest> TicketHandoffRequests => Set<TicketHandoffRequest>();

    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    public DbSet<Domain.Feedback.Feedback> FeedbackEntries => Set<Domain.Feedback.Feedback>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

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
            entity.Property(c => c.ApplicationUserId);
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

            entity.HasIndex(c => c.ApplicationUserId)
                .IsUnique()
                .HasFilter("[ApplicationUserId] IS NOT NULL");

            entity.HasOne(c => c.ApplicationUser)
                .WithMany()
                .HasForeignKey(c => c.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

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

        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.ToTable("TicketCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(c => c.Description)
                .HasMaxLength(500);
            entity.Property(c => c.IsActive)
                .IsRequired();
            entity.Property(c => c.CreatedAtUtc)
                .IsRequired();
            entity.Property(c => c.UpdatedAtUtc)
                .IsRequired();
            entity.Property(c => c.RowVersion)
                .IsRowVersion();

            entity.HasIndex(c => c.Name)
                .IsUnique();
        });

        modelBuilder.Entity<TicketPriority>(entity =>
        {
            entity.ToTable("TicketPriorities");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(p => p.SortOrder)
                .IsRequired();
            entity.Property(p => p.IsActive)
                .IsRequired();
            entity.Property(p => p.CreatedAtUtc)
                .IsRequired();
            entity.Property(p => p.UpdatedAtUtc)
                .IsRequired();
            entity.Property(p => p.RowVersion)
                .IsRowVersion();

            entity.HasIndex(p => p.Name)
                .IsUnique();
            entity.HasIndex(p => p.SortOrder);
        });

        modelBuilder.Entity<FaqEntry>(entity =>
        {
            entity.ToTable("FaqEntries");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Topic)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(f => f.Question)
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(f => f.Answer)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(f => f.SortOrder)
                .IsRequired();
            entity.Property(f => f.IsActive)
                .IsRequired();
            entity.Property(f => f.CreatedAtUtc)
                .IsRequired();
            entity.Property(f => f.UpdatedAtUtc)
                .IsRequired();
            entity.Property(f => f.RowVersion)
                .IsRowVersion();

            entity.HasIndex(f => new { f.Topic, f.SortOrder });
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Subject)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(t => t.Description)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(t => t.Status)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(t => t.IsEscalated)
                .IsRequired();
            entity.Property(t => t.CreatedAtUtc)
                .IsRequired();
            entity.Property(t => t.UpdatedAtUtc)
                .IsRequired();
            entity.Property(t => t.RowVersion)
                .IsRowVersion();

            entity.HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Category)
                .WithMany(c => c.Tickets)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Priority)
                .WithMany(p => p.Tickets)
                .HasForeignKey(t => t.PriorityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.CustomerId, t.CreatedAtUtc });
            entity.HasIndex(t => new { t.Status, t.CreatedAtUtc });
            entity.HasIndex(t => new { t.AssignedToUserId, t.Status, t.CreatedAtUtc });
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.PriorityId);
        });

        modelBuilder.Entity<TicketHistoryEntry>(entity =>
        {
            entity.ToTable("TicketHistoryEntries");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.ActionType)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(h => h.FieldName)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(h => h.OldValue)
                .HasMaxLength(500);
            entity.Property(h => h.NewValue)
                .HasMaxLength(500);
            entity.Property(h => h.ActorEmail)
                .HasMaxLength(320);
            entity.Property(h => h.OccurredAtUtc)
                .IsRequired();

            entity.HasOne(h => h.Ticket)
                .WithMany(t => t.HistoryEntries)
                .HasForeignKey(h => h.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => new { h.TicketId, h.OccurredAtUtc });
        });

        modelBuilder.Entity<TicketTask>(entity =>
        {
            entity.ToTable("TicketTasks");
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Description)
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(task => task.DueAtUtc)
                .IsRequired();
            entity.Property(task => task.Status)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(task => task.CreatedAtUtc)
                .IsRequired();
            entity.Property(task => task.UpdatedAtUtc)
                .IsRequired();
            entity.Property(task => task.CompletedAtUtc);
            entity.Property(task => task.RowVersion)
                .IsRowVersion();

            entity.HasOne(task => task.Ticket)
                .WithMany()
                .HasForeignKey(task => task.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(task => task.CreatedByUser)
                .WithMany()
                .HasForeignKey(task => task.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.AssignedToUser)
                .WithMany()
                .HasForeignKey(task => task.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.CompletedByUser)
                .WithMany()
                .HasForeignKey(task => task.CompletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(task => new { task.AssignedToUserId, task.Status, task.DueAtUtc });
            entity.HasIndex(task => new { task.TicketId, task.Status, task.DueAtUtc });
            entity.HasIndex(task => new { task.CreatedByUserId, task.CreatedAtUtc });
        });

        modelBuilder.Entity<QuickReply>(entity =>
        {
            entity.ToTable("QuickReplies");
            entity.HasKey(reply => reply.Id);
            entity.Property(reply => reply.Title)
                .HasMaxLength(120)
                .IsRequired();
            entity.Property(reply => reply.Body)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(reply => reply.TagsCsv)
                .HasMaxLength(500);
            entity.Property(reply => reply.IsActive)
                .IsRequired();
            entity.Property(reply => reply.CreatedAtUtc)
                .IsRequired();
            entity.Property(reply => reply.UpdatedAtUtc)
                .IsRequired();
            entity.Property(reply => reply.RowVersion)
                .IsRowVersion();

            entity.HasOne(reply => reply.CreatedByUser)
                .WithMany()
                .HasForeignKey(reply => reply.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reply => reply.UpdatedByUser)
                .WithMany()
                .HasForeignKey(reply => reply.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(reply => reply.Title)
                .IsUnique();
            entity.HasIndex(reply => new { reply.IsActive, reply.Title });
        });

        modelBuilder.Entity<TicketInternalNote>(entity =>
        {
            entity.ToTable("TicketInternalNotes");
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Body)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(note => note.CreatedAtUtc)
                .IsRequired();
            entity.Property(note => note.UpdatedAtUtc)
                .IsRequired();
            entity.Property(note => note.RowVersion)
                .IsRowVersion();

            entity.HasOne(note => note.Ticket)
                .WithMany()
                .HasForeignKey(note => note.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(note => note.AuthorUser)
                .WithMany()
                .HasForeignKey(note => note.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(note => new { note.TicketId, note.CreatedAtUtc });
        });

        modelBuilder.Entity<TicketInternalNoteMention>(entity =>
        {
            entity.ToTable("TicketInternalNoteMentions");
            entity.HasKey(mention => mention.Id);
            entity.Property(mention => mention.MentionedAtUtc)
                .IsRequired();
            entity.Property(mention => mention.NotificationDelivered)
                .IsRequired();
            entity.Property(mention => mention.NotificationDeliveredAtUtc);

            entity.HasOne(mention => mention.TicketInternalNote)
                .WithMany(note => note.Mentions)
                .HasForeignKey(mention => mention.TicketInternalNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mention => mention.MentionedUser)
                .WithMany()
                .HasForeignKey(mention => mention.MentionedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(mention => new { mention.TicketInternalNoteId, mention.MentionedUserId })
                .IsUnique();
            entity.HasIndex(mention => new { mention.MentionedUserId, mention.MentionedAtUtc });
        });

        modelBuilder.Entity<TicketHandoffRequest>(entity =>
        {
            entity.ToTable("TicketHandoffRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Status)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(request => request.Message)
                .HasMaxLength(1000);
            entity.Property(request => request.ResponseMessage)
                .HasMaxLength(1000);
            entity.Property(request => request.RequestedAtUtc)
                .IsRequired();
            entity.Property(request => request.RespondedAtUtc);

            entity.HasOne(request => request.Ticket)
                .WithMany()
                .HasForeignKey(request => request.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.Note)
                .WithMany()
                .HasForeignKey(request => request.NoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(request => request.RequestedByUser)
                .WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.TargetAssigneeUser)
                .WithMany()
                .HasForeignKey(request => request.TargetAssigneeUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(request => new { request.TargetAssigneeUserId, request.Status, request.RequestedAtUtc });
            entity.HasIndex(request => new { request.TicketId, request.RequestedAtUtc });
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.ToTable("TicketMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SenderType)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(message => message.SenderDisplayName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(message => message.Body)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(message => message.CreatedAtUtc)
                .IsRequired();
            entity.Property(message => message.RowVersion)
                .IsRowVersion();

            entity.HasOne(message => message.Ticket)
                .WithMany()
                .HasForeignKey(message => message.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(message => message.SenderUser)
                .WithMany()
                .HasForeignKey(message => message.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(message => new { message.TicketId, message.CreatedAtUtc, message.Id });
            entity.HasIndex(message => new { message.SenderUserId, message.CreatedAtUtc });
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.ToTable("TicketAttachments");
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.OriginalFileName)
                .HasMaxLength(260)
                .IsRequired();
            entity.Property(attachment => attachment.ContentType)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(attachment => attachment.SizeBytes)
                .IsRequired();
            entity.Property(attachment => attachment.StorageKey)
                .HasMaxLength(400)
                .IsRequired();
            entity.Property(attachment => attachment.UploadedByUserId)
                .IsRequired();
            entity.Property(attachment => attachment.UploadedByDisplayName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(attachment => attachment.CreatedAtUtc)
                .IsRequired();

            entity.HasOne(attachment => attachment.Ticket)
                .WithMany()
                .HasForeignKey(attachment => attachment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(attachment => new { attachment.TicketId, attachment.CreatedAtUtc });
        });

        modelBuilder.Entity<Domain.Feedback.Feedback>(entity =>
        {
            entity.ToTable("Feedback");
            entity.HasKey(feedback => feedback.Id);
            entity.Property(feedback => feedback.Rating)
                .IsRequired();
            entity.Property(feedback => feedback.Comment)
                .HasMaxLength(2000);
            entity.Property(feedback => feedback.CreatedAtUtc)
                .IsRequired();

            entity.HasOne(feedback => feedback.Customer)
                .WithMany()
                .HasForeignKey(feedback => feedback.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(feedback => new { feedback.CustomerId, feedback.CreatedAtUtc });
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("UserNotifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Type)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(notification => notification.PayloadJson)
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(notification => notification.CreatedAtUtc)
                .IsRequired();
            entity.Property(notification => notification.ReadAtUtc);

            entity.HasOne(notification => notification.RecipientUser)
                .WithMany()
                .HasForeignKey(notification => notification.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc });
            entity.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc });
        });
    }
}
