using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gochs.Modules.Training.Models;

public class TrainingGroup
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }

    [Required, MaxLength(150)]
    public string LeaderName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? LeaderPosition { get; set; }

    [JsonIgnore]
    public List<Employee> Employees { get; set; } = new();

    [JsonIgnore]
    public List<TrainingSession> Sessions { get; set; } = new();
}
