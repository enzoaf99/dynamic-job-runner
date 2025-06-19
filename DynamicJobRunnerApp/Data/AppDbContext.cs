using DynamicJobRunnerApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicJobRunnerApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();
    public DbSet<JobSchedule> JobSchedules => Set<JobSchedule>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}