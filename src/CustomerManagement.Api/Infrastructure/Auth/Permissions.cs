namespace CustomerManagement.Api.Infrastructure.Auth;

public static class Permissions
{
    public const string UsersManage = "admin.users.manage";
    public const string RolesManage = "admin.roles.manage";
    public const string PermissionsManage = "admin.permissions.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";

    public const string TicketsRead = "tickets.read";
    public const string TicketsWrite = "tickets.write";
    public const string TicketsAssign = "tickets.assign";
    public const string TicketsEscalate = "tickets.escalate";
    public const string TicketsClose = "tickets.close";
    public const string TicketTaxonomyManage = "tickets.taxonomy.manage";

    public const string DashboardRead = "dashboard.read";
    public const string DashboardCustomerContextRead = "dashboard.customer-context.read";

    public const string TicketTasksRead = "ticket-tasks.read";
    public const string TicketTasksWrite = "ticket-tasks.write";
    public const string TicketTasksComplete = "ticket-tasks.complete";

    public const string QuickRepliesRead = "quick-replies.read";
    public const string QuickRepliesManage = "quick-replies.manage";

    public const string TicketInternalNotesRead = "ticket-notes.read";
    public const string TicketInternalNotesWrite = "ticket-notes.write";
    public const string TicketMentionsNotify = "ticket-mentions.notify";
    public const string TicketHandoffRequestCreate = "ticket-handoff.request.create";
    public const string TicketHandoffRespond = "ticket-handoff.respond";
    public const string TicketHandoffForceAssign = "ticket-handoff.force-assign";

    public const string TicketMessagesRead = "ticket-messages.read";
    public const string TicketMessagesWrite = "ticket-messages.write";

    public const string FaqRead = "faq.read";
    public const string FaqManage = "faq.manage";

    public const string HelpArticlesRead = "help-articles.read";
    public const string HelpArticlesManage = "help-articles.manage";

    public const string GuidesRead = "guides.read";
    public const string GuidesManage = "guides.manage";

    public const string KnowledgeBaseSearch = "knowledge-base.search";

    public const string FeedbackSubmit = "feedback.submit";
    public const string FeedbackRead = "feedback.read";
}
