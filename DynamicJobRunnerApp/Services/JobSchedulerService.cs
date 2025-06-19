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
            _logger.LogDebug("Scheduler obtenido correctamente");
            return scheduler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el scheduler");
            throw;
        }
    }

    public async Task ScheduleJob(JobDefinition job)
    {
        try
        {
            _logger.LogInformation("Iniciando programación del job {JobId} ({JobName})", job.Id, job.Name);
            
            var scheduler = await GetScheduler();
            
            var jobDetail = JobBuilder.Create<QuartzJobWrapper>()
                .WithIdentity(job.Id.ToString())
                .UsingJobData("jobId", job.Id.ToString())
                .WithDescription(job.Name)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger_{job.Id}")
                .WithCronSchedule(job.CronExpression)
                .WithDescription($"Trigger para {job.Name}")
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger);
            
            _logger.LogInformation("Job {JobId} ({JobName}) programado exitosamente con cron {CronExpression}", 
                job.Id, job.Name, job.CronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al programar el job {JobId} ({JobName})", job.Id, job.Name);
            throw;
        }
    }

    public async Task InitializeJobs()
    {
        try
        {
            _logger.LogInformation("Iniciando inicialización de jobs");
            
            var jobs = await _db.JobDefinitions
                .Where(j => j.IsActive)
                .ToListAsync();
            
            _logger.LogInformation("Encontrados {JobCount} jobs activos para inicializar", jobs.Count);
            
            foreach (var job in jobs)
            {
                try
                {
                    await ScheduleJob(job);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al inicializar el job {JobId} ({JobName}). Continuando con el siguiente job", 
                        job.Id, job.Name);
                }
            }
            
            _logger.LogInformation("Finalizada la inicialización de jobs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la inicialización de jobs");
            throw;
        }
    }
    
    public async Task UnscheduleJob(Guid jobId)
    {
        try
        {
            _logger.LogInformation("Iniciando desprogramación del job {JobId}", jobId);
            
            var job = await _db.JobDefinitions.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("No se encontró el job {JobId} en la base de datos", jobId);
                return;
            }

            var scheduler = await GetScheduler();
            var jobKey = new JobKey(jobId.ToString());
            var triggerKey = new TriggerKey($"trigger_{jobId}");

            // Verificar si el job existe en el scheduler
            var jobExists = await scheduler.CheckExists(jobKey);
            if (!jobExists)
            {
                _logger.LogWarning("El job {JobId} ({JobName}) no existe en el scheduler", jobId, job.Name);
                return;
            }

            // Pausar el trigger
            _logger.LogDebug("Pausando trigger para job {JobId} ({JobName})", jobId, job.Name);
            await scheduler.PauseTrigger(triggerKey);
            
            // Eliminar trigger y job
            _logger.LogDebug("Eliminando trigger y job {JobId} ({JobName})", jobId, job.Name);
            await scheduler.UnscheduleJob(triggerKey);
            var jobDeleted = await scheduler.DeleteJob(jobKey);

            if (jobDeleted)
            {
                _logger.LogInformation("Job {JobId} ({JobName}) desprogramado exitosamente", jobId, job.Name);
            }
            else
            {
                _logger.LogWarning("No se pudo eliminar el job {JobId} ({JobName})", jobId, job.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desprogramar el job {JobId}", jobId);
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
                _logger.LogInformation("Ejecución del Job {JobId} interrumpida con éxito.", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al interrumpir la ejecución del Job {JobId}.", jobId);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("La ejecución del Job {JobId} no existe en el Scheduler.", jobId);
        }
    }
}