namespace CustomerManagement.Api.Domain.KnowledgeBase;

public sealed class GuideStep
{
    public Guid Id { get; set; }

    public Guid GuideId { get; set; }

    public int StepNumber { get; set; }

    public string Instruction { get; set; } = string.Empty;
}
