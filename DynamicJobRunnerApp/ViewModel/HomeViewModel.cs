namespace DynamicJobRunnerApp.ViewModel;

public class HomeViewModel
{
    public int TotalJobs { get; set; }
    public int FailedJobsThisWeek { get; set; }
    public int ActiveJobs { get; set; }
    public int CompletedJobsLastWeek { get; set; }
    public int RunningJobs { get; set; }
    public List<string> LastEnqueuedJobs { get; set; } = new();
}