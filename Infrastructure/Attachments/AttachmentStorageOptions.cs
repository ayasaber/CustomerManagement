namespace CustomerManagement.Api.Infrastructure.Attachments;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";

    public string Provider { get; set; } = "LocalFileSystem";

    public string RootPath { get; set; } = "App_Data/attachments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public List<string> AllowedContentTypes { get; set; } =
    [
        "text/plain",
        "application/pdf",
        "image/png",
        "image/jpeg",
        "application/json"
    ];
}
