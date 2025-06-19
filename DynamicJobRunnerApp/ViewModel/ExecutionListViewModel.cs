using DynamicJobRunnerApp.Models;

namespace DynamicJobRunnerApp.ViewModel;

public class ExecutionListViewModel
{
    public List<JobSchedule> Executions { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}