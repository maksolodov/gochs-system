using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gochs.Modules.Training.Models;

public class AttendanceRecord
{
    public int Id { get; set; }

    public int TrainingSessionId { get; set; }
    [JsonIgnore]
    public TrainingSession? TrainingSession { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public AttendanceStatus Status { get; set; }

    public TrainingTestResult TestResult { get; set; } = TrainingTestResult.None;

    public decimal? TestScore { get; set; }

    [MaxLength(30)]
    public string Source { get; set; } = "Manual";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
