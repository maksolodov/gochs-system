using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gochs.Modules.Training.Models;

public class TrainingChangeRequest
{
    public int Id { get; set; }

    public int TrainingSessionId { get; set; }
    [JsonIgnore]
    public TrainingSession? TrainingSession { get; set; }

    [Required, MaxLength(150)]
    public string RequestedBy { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
