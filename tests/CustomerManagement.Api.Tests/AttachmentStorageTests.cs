using System.Text;
using CustomerManagement.Api.Infrastructure.Attachments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace CustomerManagement.Api.Tests;

public sealed class AttachmentStorageTests : IDisposable
{
    private readonly string _tempRoot;

    public AttachmentStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"crm-attachments-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task SaveOpenDelete_Works_ForLocalFileSystemStorage()
    {
        var storage = CreateStorage();
        await using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes("hello attachment"));

        var saved = await storage.SaveAsync("note.txt", "text/plain", sourceStream);
        Assert.False(string.IsNullOrWhiteSpace(saved.StorageKey));
        Assert.Equal("note.txt", saved.OriginalFileName);
        Assert.Equal("text/plain", saved.ContentType);
        Assert.True(saved.SizeBytes > 0);

        var opened = await storage.OpenReadAsync(saved.StorageKey);
        await using (opened.Content)
        {
            using var reader = new StreamReader(opened.Content, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            Assert.Equal("hello attachment", content);
        }

        var deleted = await storage.DeleteAsync(saved.StorageKey);
        Assert.True(deleted);

        var deletedAgain = await storage.DeleteAsync(saved.StorageKey);
        Assert.False(deletedAgain);
    }

    [Fact]
    public async Task OpenReadAsync_Rejects_PathTraversalKeys()
    {
        var storage = CreateStorage();
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.OpenReadAsync("../outside.txt"));
    }

    private IAttachmentStorage CreateStorage()
    {
        var options = Options.Create(new AttachmentStorageOptions
        {
            RootPath = _tempRoot
        });

        var env = new TestWebHostEnvironment
        {
            ContentRootPath = _tempRoot,
            WebRootPath = _tempRoot
        };

        return new LocalFileSystemAttachmentStorage(options, env);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
