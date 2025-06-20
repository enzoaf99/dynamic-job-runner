using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Enums;
using DynamicJobRunnerApp.Models;
using DynamicJobRunnerApp.Services;
using DynamicJobRunnerApp.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DynamicJobRunnerApp.Controllers;

[Authorize]
public class JobsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IJobSchedulerService _jobScheduler;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        AppDbContext db,
        IJobSchedulerService jobScheduler,
        ILogger<JobsController> logger)
    {
        _db = db;
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var jobs = await _db.JobDefinitions
                .OrderByDescending(j => j.IsActive)
                .ThenBy(j => j.Name)
                .ToListAsync();

            if (!jobs.Any())
            {
                _logger.LogInformation("No jobs found");
            }
            else
            {
                _logger.LogInformation("{JobCount} jobs found", jobs.Count);
            }

            return View(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job list");
            TempData["Error"] = "Error loading jobs";
            return View(new List<JobDefinition>());
        }
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(JobDefinition job)
    {
        try
        {
            _logger.LogInformation("Attempting to create new job: {JobName}", job.Name);

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            // Validate cron expression
            CronExpression.ValidateExpression(job.CronExpression);

            job.IsActive = true; // Default to active
            _db.JobDefinitions.Add(job);
            await _db.SaveChangesAsync();

            await _jobScheduler.ScheduleJob(job);

            _logger.LogInformation("Job successfully created: {JobId} ({JobName})", job.Id, job.Name);
            TempData["Message"] = "Job successfully created";

            return RedirectToAction("Index");
        }
        catch (FormatException)
        {
            _logger.LogWarning("Invalid cron expression for job: {JobName}", job.Name);
            ModelState.AddModelError("CronExpression", "The cron expression is not valid");
            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job: {JobName}", job.Name);
            ModelState.AddModelError("", $"Error creating job: {ex.Message}");
            return View(job);
        }
    }

    public async Task<IActionResult> History(Guid id)
    {
        try
        {
            _logger.LogInformation("Fetching history for job {JobId}", id);

            var job = await _db.JobDefinitions.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("Job not found: {JobId}", id);
                return NotFound();
            }

            var history = await _db.JobSchedules
                .Where(x => x.JobDefinitionId == id)
                .OrderByDescending(x => x.EnqueuedAt)
                .ToListAsync();

            ViewBag.JobName = job.Name;
            return View(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job history {JobId}", id);
            TempData["Error"] = "Error loading job history";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        try
        {
            var job = await _db.JobDefinitions.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("Attempt to toggle non-existent job status: {JobId}", id);
                return NotFound();
            }

            _logger.LogInformation("Toggling job {JobId} ({JobName}) status from {OldStatus} to {NewStatus}",
                id, job.Name, job.IsActive, !job.IsActive);

            job.IsActive = !job.IsActive;

            if (job.IsActive)
            {
                await _jobScheduler.ScheduleJob(job);
            }
            else
            {
                await _jobScheduler.UnscheduleJob(id);
            }

            await _db.SaveChangesAsync();

            TempData["Message"] = $"Job {(job.IsActive ? "activated" : "deactivated")} successfully";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling job status {JobId}", id);
            TempData["Error"] = "Error toggling job status";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        try
        {
            _logger.LogInformation("Attempting to edit job: {JobId}", id);

            var job = await _db.JobDefinitions.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("Job not found for editing: {JobId}", id);
                return NotFound();
            }

            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job {JobId} for editing", id);
            TempData["Error"] = "Error loading job";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, JobDefinition job)
    {
        if (id != job.Id)
        {
            _logger.LogWarning("Mismatched ID when editing job: {JobId}", id);
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(job);
        }

        try
        {
            // Validate cron expression
            CronExpression.ValidateExpression(job.CronExpression);

            var existingJob = await _db.JobDefinitions.FindAsync(id);
            if (existingJob == null)
            {
                _logger.LogWarning("Job not found when saving changes: {JobId}", id);
                return NotFound();
            }

            // Update properties
            existingJob.Name = job.Name;
            existingJob.CronExpression = job.CronExpression;
            existingJob.Script = job.Script;

            // If job is active, update it in the scheduler
            if (existingJob.IsActive)
            {
                await _jobScheduler.UnscheduleJob(id);
                await _jobScheduler.ScheduleJob(existingJob);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Job successfully updated: {JobId}", id);
            TempData["Message"] = "Job successfully updated";
            return RedirectToAction(nameof(Index));
        }
        catch (FormatException)
        {
            _logger.LogWarning("Invalid cron expression when editing job: {JobId}", id);
            ModelState.AddModelError("CronExpression", "The cron expression is not valid");
            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job: {JobId}", id);
            ModelState.AddModelError("", $"Error updating job: {ex.Message}");
            return View(job);
        }
    }

    public async Task<IActionResult> Executions(int page = 1)
    {
        const int pageSize = 20;

        try
        {
            // Fetch executions ordered by most recent
            var totalExecutions = await _db.JobSchedules.CountAsync();
            var executions = await _db.JobSchedules
                .OrderByDescending(x => x.EnqueuedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(e => e.JobDefinition)
                .ToListAsync();

            var viewModel = new ExecutionListViewModel
            {
                Executions = executions,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalExecutions / (double)pageSize),
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading executions.");
            TempData["Error"] = "Error loading executions.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> CancelExecution(Guid id)
    {
        try
        {
            var execution = await _db.JobSchedules
                .Include(e => e.JobDefinition)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (execution == null || execution.Status != JobStatus.Running)
            {
                TempData["Error"] = "Cannot cancel the execution.";
                return RedirectToAction(nameof(Executions));
            }

            // Cancel job using the service
            await _jobScheduler.CancelExecution(execution.JobDefinition.Id);

            // Change status to Canceled
            execution.Status = JobStatus.Canceled;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Execution successfully canceled.";
            return RedirectToAction(nameof(Executions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attempting to cancel execution.");
            TempData["Error"] = "Error canceling the execution.";
            return RedirectToAction(nameof(Executions));
        }
    }

    public async Task<IActionResult> Home()
    {
        var totalJobs = await _db.JobDefinitions.CountAsync();
        var lastWeek = DateTime.UtcNow.AddDays(-7);
        var failedJobs = await _db.JobSchedules
            .CountAsync(s => s.Status == Enums.JobStatus.Failed && s.EnqueuedAt >= lastWeek);
        var activeJobs = await _db.JobDefinitions.CountAsync(j => j.IsActive);
        var completedJobsLastWeek = await _db.JobSchedules
            .CountAsync(s => s.Status == Enums.JobStatus.Success && s.EnqueuedAt >= lastWeek);
        var runningJobs = await _db.JobSchedules.CountAsync(s => s.Status == Enums.JobStatus.Running);

        var lastEnqueuedJobs = await _db.JobSchedules
            .OrderByDescending(s => s.EnqueuedAt)
            .Take(5)
            .Select(s => new { s.JobDefinition.Name, s.EnqueuedAt })
            .ToListAsync();

        var model = new HomeViewModel
        {
            TotalJobs = totalJobs,
            FailedJobsThisWeek = failedJobs,
            ActiveJobs = activeJobs,
            CompletedJobsLastWeek = completedJobsLastWeek,
            RunningJobs = runningJobs,
            LastEnqueuedJobs = lastEnqueuedJobs
                .Select(j => $"{j.Name}, enqueued at {j.EnqueuedAt:yyyy-MM-dd HH:mm}")
                .ToList()
        };

        return View(model);
    }
}