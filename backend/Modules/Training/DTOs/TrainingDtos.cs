using System.ComponentModel.DataAnnotations;
using Gochs.Modules.Training.Models;

namespace Gochs.Modules.Training.DTOs;

public class CreateTrainingGroupDto
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required, MaxLength(150)]
    public string LeaderName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? LeaderPosition { get; set; }
}

public class CreateEmployeeDto
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Position { get; set; }

    public PersonnelStatus PersonnelStatus { get; set; } = PersonnelStatus.Active;

    [Range(1, int.MaxValue)]
    public int TrainingGroupId { get; set; }
}

public class UpdateEmployeeStatusDto
{
    public PersonnelStatus PersonnelStatus { get; set; }
}

public class TrainingSessionUpsertDto
{
    [Range(1, int.MaxValue)]
    public int TrainingGroupId { get; set; }

    [Range(1, 1000)]
    public int TopicNumber { get; set; }

    [Required, MaxLength(300)]
    public string TopicName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    [Range(1, 24)]
    public int Hours { get; set; }

    [Required, MaxLength(100)]
    public string ClassType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Location { get; set; }

    [Required, MaxLength(150)]
    public string LeaderName { get; set; } = string.Empty;
}

public class AttendanceUpsertDto
{
    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    public AttendanceStatus Status { get; set; }

    public TrainingTestResult TestResult { get; set; } = TrainingTestResult.None;

    [Range(0, 100)]
    public decimal? TestScore { get; set; }
}

public class LmsResultDto
{
    [Range(1, int.MaxValue)]
    public int TrainingSessionId { get; set; }

    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    public bool Passed { get; set; }

    [Range(0, 100)]
    public decimal? Score { get; set; }
}

public class SignSessionDto
{
    [Required, MaxLength(150)]
    public string SignedBy { get; set; } = string.Empty;
}

public class CreateChangeRequestDto
{
    [Required, MaxLength(150)]
    public string RequestedBy { get; set; } = string.Empty;

    [Required, MinLength(5), MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class ResolveChangeRequestDto
{
    public bool Approved { get; set; }

    [Required, MaxLength(150)]
    public string ResolvedBy { get; set; } = string.Empty;
}

public class EmployeeProgressDto
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int AttendedHours { get; set; }
    public TrainingTestResult TestResult { get; set; }
}

public class TrainingSummaryDto
{
    public int TrainingGroupId { get; set; }
    public int Year { get; set; }
    public int RequiredAnnualHours { get; set; }
    public int ProtocolHours { get; set; }
    public int ConductedHours { get; set; }
    public bool Critical { get; set; }
    public bool ProtocolReady { get; set; }
    public List<EmployeeProgressDto> Employees { get; set; } = new();
}

public class ProtocolEmployeeDto
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int AttendedHours { get; set; }
    public TrainingTestResult TestResult { get; set; }
}

public class TrainingProtocolDto
{
    public int TrainingGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool Ready { get; set; }
    public bool CommitteeSignatureRequired { get; set; } = true;
    public int ConductedHours { get; set; }
    public List<ProtocolEmployeeDto> Employees { get; set; } = new();
}

public class TrainingReminderDto
{
    public int TrainingSessionId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public string? Location { get; set; }
}
