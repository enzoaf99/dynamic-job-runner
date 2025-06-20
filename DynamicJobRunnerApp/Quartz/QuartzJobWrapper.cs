using System.Threading;
using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Enums;
using DynamicJobRunnerApp.Models;
using Quartz;

namespace DynamicJobRunnerApp.Quartz;

public class QuartzJobWrapper : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuartzJobWrapper> _logger;

    public QuartzJobWrapper(IServiceProvider serviceProvider, ILogger<QuartzJobWrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("=== Starting Job Execution ===");
        _logger.LogInformation("JobKey: {JobKey}", context.JobDetail.Key);
        _logger.LogInformation("Description: {Description}", context.JobDetail.Description);

        Guid jobId;
        string jobName;

        try
        {
            var jobDataMap = context.JobDetail.JobDataMap;

            if (!jobDataMap.ContainsKey("jobId"))
            {
                _logger.LogError("jobId not found in JobDataMap");
                return;
            }

            var jobIdString = jobDataMap.GetString("jobId");
            if (!Guid.TryParse(jobIdString, out jobId))
            {
                _logger.LogError("Invalid JobId: {JobId}", jobIdString);
                return;
            }

            jobName = jobDataMap.GetString("jobName") ?? "Unknown";

            _logger.LogInformation("Processing job: {JobName} (ID: {JobId})", jobName, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job information from context");
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.JobDefinitions.FindAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Job definition not found for {JobName} (ID: {JobId})", jobName, jobId);
            return;
        }

        var schedule = new JobSchedule
        {
            JobDefinitionId = job.Id,
            EnqueuedAt = DateTime.UtcNow,
            Status = JobStatus.Running
        };

        try
        {
            db.JobSchedules.Add(schedule);
            await db.SaveChangesAsync();

            _logger.LogInformation("Executing script for job {JobName} (ID: {JobId})", job.Name, jobId);

            var (output, error, code) = await ExecuteJobWithInterruptions(context, job.Script);

            schedule.ExecutedAt = DateTime.UtcNow;
            schedule.Status = code == 0 ? JobStatus.Success : JobStatus.Failed;
            schedule.Output = output;
            schedule.Error = error;

            _logger.LogInformation("Job {JobName} (ID: {JobId}) completed with code: {ExitCode}", job.Name, jobId, code);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Execution interrupted for Job {JobName} (ID: {JobId}).", job.Name, jobId);

            schedule.ExecutedAt = DateTime.UtcNow;
            schedule.Status = JobStatus.Canceled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobName} (ID: {JobId})", job.Name, jobId);

            schedule.ExecutedAt = DateTime.UtcNow;
            schedule.Status = JobStatus.Failed;
            schedule.Error = ex.ToString();
        }
        finally
        {
            try
            {
                await db.SaveChangesAsync();
                _logger.LogInformation("Job result saved successfully for {JobName} (ID: {JobId})", job.Name, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving job result for {JobName} (ID: {JobId})", job.Name, jobId);
                throw;
            }
        }
    }

    private async Task<(string Output, string Error, int ExitCode)> ExecuteJobWithInterruptions(IJobExecutionContext context, string script)
    {
        var output = string.Empty;
        var error = string.Empty;

        for (var i = 0; i < 10; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("The job has been interrupted.");
                throw new OperationCanceledException("Job interrupted.");
            }

            _logger.LogInformation("Executing part {Step}", i + 1);
            await Task.Delay(1000); // Simulate prolonged work
        }

        return (output, error, 0); // Assume success
    }
}