using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Models;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DynamicJobRunnerApp.Quartz;

public class JobInitializer : IHostedService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobInitializer> _logger;

    public JobInitializer(
        ISchedulerFactory schedulerFactory,
        IServiceProvider serviceProvider,
        ILogger<JobInitializer> logger)
    {
        _schedulerFactory = schedulerFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting job scheduling...");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            var jobs = await db.JobDefinitions
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {JobCount} jobs to schedule", jobs.Count);

            foreach (var jobDefinition in jobs)
            {
                try
                {
                    await ScheduleJob(scheduler, jobDefinition, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error while scheduling job {JobName} (ID: {JobId})", 
                        jobDefinition.Name, 
                        jobDefinition.Id);
                }
            }

            await scheduler.Start(cancellationToken);
            _logger.LogInformation("Jobs initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job initialization");
            throw;
        }
    }

    private async Task ScheduleJob(IScheduler scheduler, JobDefinition jobDefinition, CancellationToken cancellationToken)
    {
        var jobKey = new JobKey(jobDefinition.Id.ToString());

        // Check if the job already exists
        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            _logger.LogInformation(
                "Job {JobName} (ID: {JobId}) already exists, replacing...", 
                jobDefinition.Name, 
                jobDefinition.Id);
            
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        var jobDataMap = new JobDataMap
        {
            { "jobId", jobDefinition.Id.ToString() },
            { "jobName", jobDefinition.Name },
            { "script", jobDefinition.Script }
        };

        var job = JobBuilder.Create<QuartzJobWrapper>()
            .WithIdentity(jobKey)
            .SetJobData(jobDataMap)
            .WithDescription($"Job: {jobDefinition.Name}")
            .StoreDurably()
            .RequestRecovery()
            .Build();

        var triggerKey = new TriggerKey($"trigger_{jobDefinition.Id}");
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(jobDefinition.CronExpression)
            .WithDescription($"Trigger for {jobDefinition.Name}")
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        
        _logger.LogInformation(
            "Job {JobName} (ID: {JobId}) successfully scheduled with cron: {CronExpression}", 
            jobDefinition.Name, 
            jobDefinition.Id, 
            jobDefinition.CronExpression);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
            _logger.LogInformation("Scheduler shut down successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while shutting down the scheduler");
            throw;
        }
    }
}