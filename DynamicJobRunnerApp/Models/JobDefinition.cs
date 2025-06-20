using System.ComponentModel.DataAnnotations;
using Quartz;

namespace DynamicJobRunnerApp.Models;

public class JobDefinition
{
    public Guid Id { get; set; }
    
    [Required(ErrorMessage = "The name is required")]
    public string Name { get; set; }
    
    [Required(ErrorMessage = "The script is required")]
    public string Script { get; set; }
    
    [Required(ErrorMessage = "The cron expression is required")]
    [CronExpression(ErrorMessage = "The cron expression is not valid")]
    public string CronExpression { get; set; }
    
    public bool IsActive { get; set; } = true;
}

// Custom attribute to validate cron expression
public class CronExpressionAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var cronExpression = value as string;
        if (string.IsNullOrEmpty(cronExpression))
            return new ValidationResult("The cron expression is required");

        try
        {
            CronExpression.ValidateExpression(cronExpression);
            return ValidationResult.Success;
        }
        catch
        {
            return new ValidationResult("The cron expression is not valid");
        }
    }
}