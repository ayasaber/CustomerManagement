namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public sealed record CreateGuideRequest(string Title, IReadOnlyList<string> Steps);

public sealed record UpdateGuideRequest(string Title, IReadOnlyList<string> Steps, bool IsActive, byte[] RowVersion);

public sealed record GuideStepResponse(int StepNumber, string Instruction);

public sealed record GuideResponse(
    Guid Id,
    string Title,
    bool IsActive,
    IReadOnlyList<GuideStepResponse> Steps,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
