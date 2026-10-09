using Gochs.Modules.Training.DTOs;
using Gochs.Modules.Training.Models;

namespace Gochs.Modules.Training.Services;

public interface ITrainingService
{
    Task<List<TrainingGroup>> GetGroupsAsync(int? year = null);
    Task<TrainingGroup> CreateGroupAsync(CreateTrainingGroupDto dto);
    Task<List<Employee>> GetEmployeesAsync(int? groupId = null);
    Task<Employee> CreateEmployeeAsync(CreateEmployeeDto dto);
    Task<Employee?> UpdateEmployeeStatusAsync(int id, UpdateEmployeeStatusDto dto);
    Task<List<TrainingSession>> GetSessionsAsync(int? groupId = null, int? year = null);
    Task<TrainingSession> CreateSessionAsync(TrainingSessionUpsertDto dto);
    Task<TrainingSession?> UpdateSessionAsync(int id, TrainingSessionUpsertDto dto);
    Task<TrainingSession?> SignSessionAsync(int id, SignSessionDto dto);
    Task<List<AttendanceRecord>> GetAttendanceAsync(int sessionId);
    Task<AttendanceRecord?> UpsertAttendanceAsync(int sessionId, AttendanceUpsertDto dto);
    Task<AttendanceRecord?> ApplyLmsResultAsync(LmsResultDto dto);
    Task<TrainingSummaryDto?> GetSummaryAsync(int groupId, int year, DateOnly referenceDate);
    Task<TrainingProtocolDto?> GetProtocolAsync(int groupId, int year);
    Task<List<TrainingReminderDto>> GetRemindersAsync(DateOnly referenceDate);
    Task<TrainingChangeRequest?> CreateChangeRequestAsync(int sessionId, CreateChangeRequestDto dto);
    Task<TrainingChangeRequest?> ResolveChangeRequestAsync(int requestId, ResolveChangeRequestDto dto);
    Task<List<TrainingAuditLog>> GetAuditAsync(int sessionId);
}
