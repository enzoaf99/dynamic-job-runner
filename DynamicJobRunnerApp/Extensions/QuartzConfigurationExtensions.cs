using Quartz;

namespace DynamicJobRunnerApp.Extensions;

public static class QuartzConfigurationExtensions
{
    public static void AddJobAndTrigger<T>(
        this IServiceCollectionQuartzConfigurator quartz,
        IConfiguration config) where T : IJob
    {
        var jobName = typeof(T).Name;

        var jobKey = new JobKey(jobName);
        quartz.AddJob<T>(opts => opts.WithIdentity(jobKey));

        quartz.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{jobName}-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(10)
                .RepeatForever())
        );
    }
}