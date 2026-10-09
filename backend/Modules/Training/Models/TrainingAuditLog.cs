using System.ComponentModel.DataAnnotations;

namespace Gochs.Modules.Training.Models;

public class TrainingAuditLog
{
    public int Id { get; set; }

    public int? TrainingSessionId { get; set; }

    public int? EmployeeId { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
