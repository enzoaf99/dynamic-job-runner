using DynamicJobRunnerApp.Data;
using DynamicJobRunnerApp.Enums;
using DynamicJobRunnerApp.Models;
using DynamicJobRunnerApp.Services;
using DynamicJobRunnerApp.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DynamicJobRunnerApp.Controllers;

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
                _logger.LogInformation("No se encontraron jobs configurados");
            }
            else
            {
                _logger.LogInformation("Se encontraron {JobCount} jobs configurados", jobs.Count);
            }

            return View(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de jobs");
            TempData["Error"] = "Error al cargar los jobs";
            return View(new List<JobDefinition>());
        }
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(JobDefinition job)
    {
        try
        {
            _logger.LogInformation("Intentando crear nuevo job: {JobName}", job.Name);

            if (!ModelState.IsValid)
            {
                return View(job);
            }

            // Validar la expresión cron
            CronExpression.ValidateExpression(job.CronExpression);

            job.IsActive = true; // Por defecto activo
            _db.JobDefinitions.Add(job);
            await _db.SaveChangesAsync();

            await _jobScheduler.ScheduleJob(job);

            _logger.LogInformation("Job creado exitosamente: {JobId} ({JobName})", job.Id, job.Name);
            TempData["Message"] = "Job creado exitosamente";

            return RedirectToAction("Index");
        }
        catch (FormatException)
        {
            _logger.LogWarning("Expresión cron inválida para job: {JobName}", job.Name);
            ModelState.AddModelError("CronExpression", "La expresión cron no es válida");
            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear job: {JobName}", job.Name);
            ModelState.AddModelError("", $"Error al crear el job: {ex.Message}");
            return View(job);
        }
    }

    public async Task<IActionResult> History(Guid id)
    {
        try
        {
            _logger.LogInformation("Consultando historial del job {JobId}", id);

            var job = await _db.JobDefinitions.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("Job no encontrado: {JobId}", id);
                return NotFound();
            }

            var historial = await _db.JobSchedules
                .Where(x => x.JobDefinitionId == id)
                .OrderByDescending(x => x.EnqueuedAt)
                .ToListAsync();

            ViewBag.JobName = job.Name;
            return View(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener historial del job {JobId}", id);
            TempData["Error"] = "Error al cargar el historial";
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
                _logger.LogWarning("Intento de cambiar estado de job inexistente: {JobId}", id);
                return NotFound();
            }

            _logger.LogInformation("Cambiando estado del job {JobId} ({JobName}) de {OldStatus} a {NewStatus}",
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

            TempData["Message"] = $"Job {(job.IsActive ? "activado" : "desactivado")} exitosamente";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado del job {JobId}", id);
            TempData["Error"] = "Error al cambiar el estado del job";
            return RedirectToAction(nameof(Index));
        }
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        try
        {
            _logger.LogInformation("Intentando editar job: {JobId}", id);

            var job = await _db.JobDefinitions.FindAsync(id);
            if (job == null)
            {
                _logger.LogWarning("Job no encontrado para editar: {JobId}", id);
                return NotFound();
            }

            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar job para editar: {JobId}", id);
            TempData["Error"] = "Error al cargar el job";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, JobDefinition job)
    {
        if (id != job.Id)
        {
            _logger.LogWarning("ID no coincide al editar job: {JobId}", id);
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(job);
        }

        try
        {
            // Validar la expresión cron
            CronExpression.ValidateExpression(job.CronExpression);

            var existingJob = await _db.JobDefinitions.FindAsync(id);
            if (existingJob == null)
            {
                _logger.LogWarning("Job no encontrado al guardar cambios: {JobId}", id);
                return NotFound();
            }

            // Actualizar propiedades
            existingJob.Name = job.Name;
            existingJob.CronExpression = job.CronExpression;
            existingJob.Script = job.Script;

            // Si el job está activo, actualizarlo en el scheduler
            if (existingJob.IsActive)
            {
                await _jobScheduler.UnscheduleJob(id);
                await _jobScheduler.ScheduleJob(existingJob);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Job actualizado exitosamente: {JobId}", id);
            TempData["Message"] = "Job actualizado correctamente";
            return RedirectToAction(nameof(Index));
        }
        catch (FormatException)
        {
            _logger.LogWarning("Expresión cron inválida al editar job: {JobId}", id);
            ModelState.AddModelError("CronExpression", "La expresión cron no es válida");
            return View(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar job: {JobId}", id);
            ModelState.AddModelError("", $"Error al actualizar el job: {ex.Message}");
            return View(job);
        }
    }
    
    public async Task<IActionResult> Executions(int page = 1)
    {
        const int pageSize = 20; // Tamaño de página

        try
        {
            // Obtener las ejecuciones ordenadas por las más recientes
            var totalExecutions = await _db.JobSchedules.CountAsync();
            var executions = await _db.JobSchedules
                .OrderByDescending(x => x.EnqueuedAt)
                .Skip((page - 1) * pageSize) // Paginación
                .Take(pageSize)
                .Include(e => e.JobDefinition) // Incluir información del Job relacionado (si existe)
                .ToListAsync();

            // Crear un modelo para enviar datos a la vista
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
            _logger.LogError(ex, "Error al cargar las ejecuciones.");
            TempData["Error"] = "Error al cargar las ejecuciones.";
            return RedirectToAction(nameof(Index));
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CancelExecution(Guid id)
    {
        try
        {
            var execution = await _db.JobSchedules
                .Include(e => e.JobDefinition) // Incluimos JobDefinition
                .FirstOrDefaultAsync(e => e.Id == id);

            if (execution == null || execution.Status != JobStatus.Running)
            {
                TempData["Error"] = "No se puede cancelar la ejecución.";
                return RedirectToAction(nameof(Executions));
            }

            // Cancelar job utilizando el servicio
            await _jobScheduler.CancelExecution(execution.JobDefinition.Id);

            // Cambiar el estado a Cancelled
            execution.Status = JobStatus.Canceled;
            await _db.SaveChangesAsync();

            TempData["Message"] = "La ejecución fue cancelada correctamente.";
            return RedirectToAction(nameof(Executions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar cancelar la ejecución.");
            TempData["Error"] = "Error al cancelar la ejecución.";
            return RedirectToAction(nameof(Executions));
        }
    }
}