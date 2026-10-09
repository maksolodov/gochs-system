using Microsoft.EntityFrameworkCore;
using Gochs.Data;
using Gochs.Modules.Training.DTOs;
using Gochs.Modules.Training.Models;

namespace Gochs.Modules.Training.Services;

public class TrainingService : ITrainingService
{
    private const int RequiredAnnualHours = 15;
    private const int ProtocolHours = 12;
    private readonly AppDbContext _db;

    public TrainingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TrainingGroup>> GetGroupsAsync(int? year = null)
    {
        var query = _db.TrainingGroups.AsNoTracking().AsQueryable();
        if (year.HasValue)
            query = query.Where(x => x.Year == year.Value);

        return await query.OrderByDescending(x => x.Year).ThenBy(x => x.Name).ToListAsync();
    }

    public async Task<TrainingGroup> CreateGroupAsync(CreateTrainingGroupDto dto)
    {
        var exists = await _db.TrainingGroups.AnyAsync(x => x.Year == dto.Year && x.Name == dto.Name);
        if (exists)
            throw new InvalidOperationException("Учебная группа с таким названием уже существует в выбранном году.");

        var group = new TrainingGroup
        {
            Name = dto.Name.Trim(),
            Year = dto.Year,
            LeaderName = dto.LeaderName.Trim(),
            LeaderPosition = dto.LeaderPosition?.Trim()
        };

        _db.TrainingGroups.Add(group);
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task<List<Employee>> GetEmployeesAsync(int? groupId = null)
    {
        var query = _db.Employees.AsNoTracking().AsQueryable();
        if (groupId.HasValue)
            query = query.Where(x => x.TrainingGroupId == groupId.Value);

        return await query.OrderBy(x => x.FullName).ToListAsync();
    }

    public async Task<Employee> CreateEmployeeAsync(CreateEmployeeDto dto)
    {
        var groupExists = await _db.TrainingGroups.AnyAsync(x => x.Id == dto.TrainingGroupId);
        if (!groupExists)
            throw new KeyNotFoundException("Учебная группа не найдена.");

        var employee = new Employee
        {
            FullName = dto.FullName.Trim(),
            Position = dto.Position?.Trim(),
            PersonnelStatus = dto.PersonnelStatus,
            TrainingGroupId = dto.TrainingGroupId
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    public async Task<Employee?> UpdateEmployeeStatusAsync(int id, UpdateEmployeeStatusDto dto)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null)
            return null;

        employee.PersonnelStatus = dto.PersonnelStatus;
        await _db.SaveChangesAsync();
        return employee;
    }

    public async Task<List<TrainingSession>> GetSessionsAsync(int? groupId = null, int? year = null)
    {
        var query = _db.TrainingSessions
            .AsNoTracking()
            .Include(x => x.TrainingGroup)
            .AsQueryable();

        if (groupId.HasValue)
            query = query.Where(x => x.TrainingGroupId == groupId.Value);

        if (year.HasValue)
            query = query.Where(x => x.TrainingGroup != null && x.TrainingGroup.Year == year.Value);

        return await query.OrderBy(x => x.Date).ThenBy(x => x.TopicNumber).ToListAsync();
    }

    public async Task<TrainingSession> CreateSessionAsync(TrainingSessionUpsertDto dto)
    {
        var group = await _db.TrainingGroups.FindAsync(dto.TrainingGroupId);
        if (group is null)
            throw new KeyNotFoundException("Учебная группа не найдена.");

        if (group.Year != dto.Date.Year)
            throw new InvalidOperationException("Год занятия должен совпадать с учебным годом группы.");

        var session = new TrainingSession
        {
            TrainingGroupId = dto.TrainingGroupId,
            TopicNumber = dto.TopicNumber,
            TopicName = dto.TopicName.Trim(),
            Date = dto.Date,
            StartTime = dto.StartTime,
            Hours = dto.Hours,
            ClassType = dto.ClassType.Trim(),
            Location = dto.Location?.Trim(),
            LeaderName = dto.LeaderName.Trim()
        };

        _db.TrainingSessions.Add(session);
        await _db.SaveChangesAsync();
        await AddAuditAsync(session.Id, null, "SessionCreated", $"Создано занятие: {session.TopicName}");
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<TrainingSession?> UpdateSessionAsync(int id, TrainingSessionUpsertDto dto)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session is null)
            return null;

        if (session.IsSigned)
            throw new InvalidOperationException("Подписанное занятие заблокировано для редактирования.");

        var group = await _db.TrainingGroups.FindAsync(dto.TrainingGroupId);
        if (group is null)
            throw new KeyNotFoundException("Учебная группа не найдена.");

        if (group.Year != dto.Date.Year)
            throw new InvalidOperationException("Год занятия должен совпадать с учебным годом группы.");

        session.TrainingGroupId = dto.TrainingGroupId;
        session.TopicNumber = dto.TopicNumber;
        session.TopicName = dto.TopicName.Trim();
        session.Date = dto.Date;
        session.StartTime = dto.StartTime;
        session.Hours = dto.Hours;
        session.ClassType = dto.ClassType.Trim();
        session.Location = dto.Location?.Trim();
        session.LeaderName = dto.LeaderName.Trim();

        await AddAuditAsync(session.Id, null, "SessionUpdated", $"Изменено занятие: {session.TopicName}");
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<TrainingSession?> SignSessionAsync(int id, SignSessionDto dto)
    {
        var session = await _db.TrainingSessions.FindAsync(id);
        if (session is null)
            return null;

        if (session.Date > Gochs.Common.Clock.Today)
            throw new InvalidOperationException("Нельзя подписать будущее занятие.");

        if (session.IsSigned)
            return session;

        session.IsSigned = true;
        session.SignedBy = dto.SignedBy.Trim();
        session.SignedAt = DateTime.UtcNow;

        await AddAuditAsync(session.Id, null, "SessionSigned", $"Занятие подписано: {session.SignedBy}");
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<List<AttendanceRecord>> GetAttendanceAsync(int sessionId)
    {
        return await _db.AttendanceRecords
            .AsNoTracking()
            .Where(x => x.TrainingSessionId == sessionId)
            .Include(x => x.Employee)
            .OrderBy(x => x.Employee != null ? x.Employee.FullName : string.Empty)
            .ToListAsync();
    }

    public async Task<AttendanceRecord?> UpsertAttendanceAsync(int sessionId, AttendanceUpsertDto dto)
    {
        var session = await _db.TrainingSessions.FindAsync(sessionId);
        if (session is null)
            return null;

        if (session.IsSigned)
            throw new InvalidOperationException("Подписанное занятие заблокировано для редактирования.");

        var employee = await _db.Employees.FindAsync(dto.EmployeeId);
        if (employee is null)
            throw new KeyNotFoundException("Сотрудник не найден.");

        if (employee.TrainingGroupId != session.TrainingGroupId)
            throw new InvalidOperationException("Сотрудник не входит в учебную группу этого занятия.");

        await ValidatePresenceDate(employee, session.Date, dto.Status);

        var record = await _db.AttendanceRecords
            .SingleOrDefaultAsync(x => x.TrainingSessionId == sessionId && x.EmployeeId == dto.EmployeeId);

        if (record is null)
        {
            record = new AttendanceRecord
            {
                TrainingSessionId = sessionId,
                EmployeeId = dto.EmployeeId
            };
            _db.AttendanceRecords.Add(record);
        }

        record.Status = dto.Status;
        record.TestResult = dto.TestResult;
        record.TestScore = dto.TestScore;
        record.Source = "Manual";
        record.UpdatedAt = DateTime.UtcNow;

        await AddAuditAsync(sessionId, dto.EmployeeId, "AttendanceUpdated", $"Статус: {record.Status}, результат: {record.TestResult}");
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<AttendanceRecord?> ApplyLmsResultAsync(LmsResultDto dto)
    {
        var session = await _db.TrainingSessions.FindAsync(dto.TrainingSessionId);
        if (session is null)
            return null;

        if (session.IsSigned)
            throw new InvalidOperationException("Подписанное занятие заблокировано для редактирования.");

        var employee = await _db.Employees.FindAsync(dto.EmployeeId);
        if (employee is null)
            throw new KeyNotFoundException("Сотрудник не найден.");

        if (employee.TrainingGroupId != session.TrainingGroupId)
            throw new InvalidOperationException("Сотрудник не входит в учебную группу этого занятия.");

        await ValidatePresenceDate(employee, session.Date, AttendanceStatus.Present);

        var record = await _db.AttendanceRecords
            .SingleOrDefaultAsync(x => x.TrainingSessionId == dto.TrainingSessionId && x.EmployeeId == dto.EmployeeId);

        if (record is null)
        {
            record = new AttendanceRecord
            {
                TrainingSessionId = dto.TrainingSessionId,
                EmployeeId = dto.EmployeeId
            };
            _db.AttendanceRecords.Add(record);
        }

        record.Status = AttendanceStatus.Present;
        record.TestResult = dto.Passed ? TrainingTestResult.Passed : TrainingTestResult.Failed;
        record.TestScore = dto.Score;
        record.Source = "LMS";
        record.UpdatedAt = DateTime.UtcNow;

        await AddAuditAsync(dto.TrainingSessionId, dto.EmployeeId, "LmsResultApplied", $"Результат: {record.TestResult}, балл: {record.TestScore}");
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task<TrainingSummaryDto?> GetSummaryAsync(int groupId, int year, DateOnly referenceDate)
    {
        var group = await _db.TrainingGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == groupId && x.Year == year);
        if (group is null)
            return null;

        var sessions = await _db.TrainingSessions
            .AsNoTracking()
            .Where(x => x.TrainingGroupId == groupId && x.Date.Year == year)
            .ToListAsync();

        var sessionIds = sessions.Select(x => x.Id).ToList();

        var attendance = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(x => sessionIds.Contains(x.TrainingSessionId))
            .Include(x => x.TrainingSession)
            .ToListAsync();

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(x => x.TrainingGroupId == groupId)
            .OrderBy(x => x.FullName)
            .ToListAsync();

        var conductedHours = sessions.Where(x => x.IsSigned).Sum(x => x.Hours);
        var critical = referenceDate.Year == year && referenceDate.Month >= 11 && conductedHours < 10;

        var progress = employees.Select(employee =>
        {
            var employeeRecords = attendance.Where(x => x.EmployeeId == employee.Id).ToList();
            var attendedHours = employeeRecords
                .Where(x => x.Status == AttendanceStatus.Present && x.TrainingSession != null && x.TrainingSession.IsSigned)
                .Sum(x => x.TrainingSession!.Hours);

            var testResult = employeeRecords
                .Where(x => x.TestResult != TrainingTestResult.None)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => x.TestResult)
                .FirstOrDefault();

            return new EmployeeProgressDto
            {
                EmployeeId = employee.Id,
                FullName = employee.FullName,
                AttendedHours = attendedHours,
                TestResult = testResult
            };
        }).ToList();

        return new TrainingSummaryDto
        {
            TrainingGroupId = groupId,
            Year = year,
            RequiredAnnualHours = RequiredAnnualHours,
            ProtocolHours = ProtocolHours,
            ConductedHours = conductedHours,
            Critical = critical,
            ProtocolReady = conductedHours >= ProtocolHours,
            Employees = progress
        };
    }

    public async Task<TrainingProtocolDto?> GetProtocolAsync(int groupId, int year)
    {
        var group = await _db.TrainingGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == groupId && x.Year == year);
        if (group is null)
            return null;

        var summary = await GetSummaryAsync(groupId, year, Gochs.Common.Clock.Today);
        if (summary is null)
            return null;

        return new TrainingProtocolDto
        {
            TrainingGroupId = groupId,
            GroupName = group.Name,
            Year = year,
            Ready = summary.ConductedHours >= ProtocolHours,
            ConductedHours = summary.ConductedHours,
            Employees = summary.Employees.Select(x => new ProtocolEmployeeDto
            {
                EmployeeId = x.EmployeeId,
                FullName = x.FullName,
                AttendedHours = x.AttendedHours,
                TestResult = x.TestResult
            }).ToList()
        };
    }

    public async Task<List<TrainingReminderDto>> GetRemindersAsync(DateOnly referenceDate)
    {
        var targetDate = referenceDate.AddDays(3);

        return await _db.TrainingSessions
            .AsNoTracking()
            .Where(x => x.Date == targetDate)
            .Include(x => x.TrainingGroup)
            .OrderBy(x => x.StartTime)
            .Select(x => new TrainingReminderDto
            {
                TrainingSessionId = x.Id,
                GroupName = x.TrainingGroup != null ? x.TrainingGroup.Name : string.Empty,
                TopicName = x.TopicName,
                Date = x.Date,
                StartTime = x.StartTime,
                Location = x.Location
            })
            .ToListAsync();
    }

    public async Task<TrainingChangeRequest?> CreateChangeRequestAsync(int sessionId, CreateChangeRequestDto dto)
    {
        var session = await _db.TrainingSessions.FindAsync(sessionId);
        if (session is null)
            return null;

        if (!session.IsSigned)
            throw new InvalidOperationException("Запрос на изменение нужен только для подписанного занятия.");

        var request = new TrainingChangeRequest
        {
            TrainingSessionId = sessionId,
            RequestedBy = dto.RequestedBy.Trim(),
            Reason = dto.Reason.Trim()
        };

        _db.TrainingChangeRequests.Add(request);
        await AddAuditAsync(sessionId, null, "ChangeRequested", $"Инициатор: {request.RequestedBy}. Причина: {request.Reason}");
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<TrainingChangeRequest?> ResolveChangeRequestAsync(int requestId, ResolveChangeRequestDto dto)
    {
        var request = await _db.TrainingChangeRequests
            .Include(x => x.TrainingSession)
            .FirstOrDefaultAsync(x => x.Id == requestId);

        if (request is null)
            return null;

        if (request.Status != ChangeRequestStatus.Pending)
            throw new InvalidOperationException("Запрос уже обработан.");

        request.Status = dto.Approved ? ChangeRequestStatus.Approved : ChangeRequestStatus.Rejected;
        request.ResolvedBy = dto.ResolvedBy.Trim();
        request.ResolvedAt = DateTime.UtcNow;

        if (dto.Approved && request.TrainingSession is not null)
        {
            request.TrainingSession.IsSigned = false;
            request.TrainingSession.SignedBy = null;
            request.TrainingSession.SignedAt = null;
        }

        await AddAuditAsync(
            request.TrainingSessionId,
            null,
            dto.Approved ? "ChangeApproved" : "ChangeRejected",
            $"Администратор: {request.ResolvedBy}");

        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<List<TrainingAuditLog>> GetAuditAsync(int sessionId)
    {
        return await _db.TrainingAuditLogs
            .AsNoTracking()
            .Where(x => x.TrainingSessionId == sessionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    private async Task ValidatePresenceDate(Employee employee, DateOnly date, AttendanceStatus status)
    {
        if (status != AttendanceStatus.Present) return;
        if (employee.PersonnelStatus == PersonnelStatus.Dismissed) throw new InvalidOperationException("Сотрудник уволен.");
        if (await _db.Leaves.AnyAsync(x => x.EmployeeId == employee.Id && x.Status == "Approved" && x.StartDate <= date && x.EndDate >= date))
            throw new InvalidOperationException("На дату занятия у сотрудника оформлено отсутствие.");
        ValidatePresentStatus(employee, status);
    }

    private static void ValidatePresentStatus(Employee employee, AttendanceStatus status)
    {
        if (status != AttendanceStatus.Present)
            return;

        if (employee.PersonnelStatus == PersonnelStatus.Vacation)
            throw new InvalidOperationException("Нельзя поставить присутствие сотруднику со статусом отпуска.");

        if (employee.PersonnelStatus == PersonnelStatus.SickLeave)
            throw new InvalidOperationException("Нельзя поставить присутствие сотруднику со статусом больничного.");
    }

    private async Task AddAuditAsync(int? sessionId, int? employeeId, string action, string? details)
    {
        await _db.TrainingAuditLogs.AddAsync(new TrainingAuditLog
        {
            TrainingSessionId = sessionId,
            EmployeeId = employeeId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
    }
}
