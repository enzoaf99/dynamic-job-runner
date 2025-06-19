using DynamicJobRunnerApp.Enums;

namespace DynamicJobRunnerApp.Models;

public class JobSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobDefinitionId { get; set; }
    public JobDefinition JobDefinition { get; set; } = default!;
    public DateTime EnqueuedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public JobStatus Status { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}