using System.ComponentModel.DataAnnotations;
using Quartz;

namespace DynamicJobRunnerApp.Models;

public class JobDefinition
{
    public Guid Id { get; set; }
    
    [Required(ErrorMessage = "El nombre es requerido")]
    public string Name { get; set; }
    
    [Required(ErrorMessage = "El script es requerido")]
    public string Script { get; set; }
    
    [Required(ErrorMessage = "La expresión cron es requerida")]
    [CronExpression(ErrorMessage = "La expresión cron no es válida")]
    public string CronExpression { get; set; }
    public bool IsActive { get; set; } = true;
}

// Atributo personalizado para validar expresión cron
public class CronExpressionAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var cronExpression = value as string;
        if (string.IsNullOrEmpty(cronExpression))
            return new ValidationResult("La expresión cron es requerida");

        try
        {
            CronExpression.ValidateExpression(cronExpression);
            return ValidationResult.Success;
        }
        catch
        {
            return new ValidationResult("La expresión cron no es válida");
        }
    }
}