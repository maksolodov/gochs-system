using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gochs.Modules.Training.Models;

public partial class Employee
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Position { get; set; }

    public PersonnelStatus PersonnelStatus { get; set; } = PersonnelStatus.Active;

    public int? TrainingGroupId { get; set; }
    [JsonIgnore]
    public TrainingGroup? TrainingGroup { get; set; }

    [JsonIgnore]
    public List<AttendanceRecord> AttendanceRecords { get; set; } = new();
}
