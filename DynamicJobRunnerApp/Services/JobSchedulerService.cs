using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Models;
using DynamicJobRunnerApp.Quartz;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DynamicJobRunnerApp.Services;

public interface IJobSchedulerService
{
    Task ScheduleJob(JobDefinition job);
    Task InitializeJobs();
    Task UnscheduleJob(Guid jobId);
    Task<IScheduler> GetScheduler();
    Task CancelExecution(Guid jobId);
}

public class JobSchedulerService : IJobSchedulerService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<JobSchedulerService> _logger;

    public JobSchedulerService(
        ISchedulerFactory schedulerFactory, 
        AppDbContext db,
        ILogger<JobSchedulerService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<IScheduler> GetScheduler()
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            _logger.LogDebug("Scheduler retrieved successfully");
            return scheduler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving the scheduler");
            throw;
        }
    }

    public async Task ScheduleJob(JobDefinition job)
    {
        try
        {
            _logger.LogInformation("Starting job scheduling {JobId} ({JobName})", job.Id, job.Name);
            
            var scheduler = await GetScheduler();
            
            var jobDetail = JobBuilder.Create<QuartzJobWrapper>()
                .WithIdentity(job.Id.ToString())
                .UsingJobData("jobId", job.Id.ToString())
                .WithDescription(job.Name)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger_{job.Id}")
                .WithCronSchedule(job.CronExpression)
                .WithDescription($"Trigger for {job.Name}")
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger);
            
            _logger.LogInformation("Job {JobId} ({JobName}) successfully scheduled with cron {CronExpression}", 
                job.Id, job.Name, job.CronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while scheduling job {JobId} ({JobName})", job.Id, job.Name);
            throw;
        }
    }

    public async Task InitializeJobs()
    {
        try
        {
            _logger.LogInformation("Starting job initialization");
            
            var jobs = await _db.JobDefinitions
                .Where(j => j.IsActive)
                .ToListAsync();
            
            _logger.LogInformation("Found {JobCount} active jobs to initialize", jobs.Count);
            
            foreach (var job in jobs)
            {
                try
                {
                    await ScheduleJob(job);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while initializing job {JobId} ({JobName}). Continuing with the next job", 
                        job.Id, job.Name);
                }
            }
            
            _logger.LogInformation("Job initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job initialization");
            throw;
        }
    }
    
    public async Task UnscheduleJob(Guid jobId)
    {
        try
        {
            _logger.LogInformation("Starting the unscheduling of job {JobId}", jobId);
            
            var job = await _db.JobDefinitions.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found in the database", jobId);
                return;
            }

            var scheduler = await GetScheduler();
            var jobKey = new JobKey(jobId.ToString());
            var triggerKey = new TriggerKey($"trigger_{jobId}");

            // Check if the job exists in the scheduler
            var jobExists = await scheduler.CheckExists(jobKey);
            if (!jobExists)
            {
                _logger.LogWarning("Job {JobId} ({JobName}) does not exist in the scheduler", jobId, job.Name);
                return;
            }

            // Pause the trigger
            _logger.LogDebug("Pausing trigger for job {JobId} ({JobName})", jobId, job.Name);
            await scheduler.PauseTrigger(triggerKey);
            
            // Remove the trigger and job
            _logger.LogDebug("Removing trigger and job {JobId} ({JobName})", jobId, job.Name);
            await scheduler.UnscheduleJob(triggerKey);
            var jobDeleted = await scheduler.DeleteJob(jobKey);

            if (jobDeleted)
            {
                _logger.LogInformation("Job {JobId} ({JobName}) successfully unscheduled", jobId, job.Name);
            }
            else
            {
                _logger.LogWarning("Failed to delete the job {JobId} ({JobName})", jobId, job.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while unscheduling the job {JobId}", jobId);
            throw;
        }
    }
    
    public async Task CancelExecution(Guid jobId)
    {
        var scheduler = await GetScheduler();

        var jobKey = new JobKey(jobId.ToString());
        if (await scheduler.CheckExists(jobKey))
        {
            try
            {
                await scheduler.Interrupt(jobKey);
                _logger.LogInformation("Job execution {JobId} successfully interrupted.", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while interrupting the execution of job {JobId}.", jobId);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Execution of Job {JobId} does not exist in the Scheduler.", jobId);
        }
    }
}