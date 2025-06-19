using DynamicJobRunnerApp.Models;

namespace DynamicJobRunnerApp.Data;

public static class DbSeeder
{
    public static void Seed(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.JobDefinitions.Any())
        {
            db.JobDefinitions.Add(new JobDefinition
            {
                Name = "Ping Google",
                CronExpression = "0 0/1 * 1/1 * ? *",
                Script = "ping -c 1 google.com"
            });

            db.SaveChanges();
        }
    }
}