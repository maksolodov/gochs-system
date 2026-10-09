using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gochs.Modules.Training.Models;

public class TrainingSession
{
    public int Id { get; set; }

    public int TrainingGroupId { get; set; }
    [JsonIgnore]
    public TrainingGroup? TrainingGroup { get; set; }

    public int TopicNumber { get; set; }

    [Required, MaxLength(300)]
    public string TopicName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public int Hours { get; set; }

    [Required, MaxLength(100)]
    public string ClassType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Location { get; set; }

    [Required, MaxLength(150)]
    public string LeaderName { get; set; } = string.Empty;

    public bool IsSigned { get; set; }

    [MaxLength(150)]
    public string? SignedBy { get; set; }

    public DateTime? SignedAt { get; set; }

    [JsonIgnore]
    public List<AttendanceRecord> AttendanceRecords { get; set; } = new();

    [JsonIgnore]
    public List<TrainingChangeRequest> ChangeRequests { get; set; } = new();
}
