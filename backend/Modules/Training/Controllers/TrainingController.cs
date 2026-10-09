using Microsoft.AspNetCore.Mvc;
using Gochs.Modules.Training.DTOs;
using Gochs.Modules.Training.Models;
using Gochs.Modules.Training.Services;

namespace Gochs.Modules.Training.Controllers;

[ApiController]
[Route("api/training")]
public class TrainingController : ControllerBase
{
    private readonly ITrainingService _service;

    public TrainingController(ITrainingService service)
    {
        _service = service;
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<TrainingGroup>>> GetGroups([FromQuery] int? year)
    {
        return Ok(await _service.GetGroupsAsync(year));
    }

    [HttpPost("groups")]
    public async Task<ActionResult<TrainingGroup>> CreateGroup(CreateTrainingGroupDto dto)
    {
        try
        {
            var group = await _service.CreateGroupAsync(dto);
            return Created($"/api/training/groups/{group.Id}", group);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("employees")]
    public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees([FromQuery] int? groupId)
    {
        return Ok(await _service.GetEmployeesAsync(groupId));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<TrainingSession>>> GetSessions([FromQuery] int? groupId, [FromQuery] int? year)
    {
        return Ok(await _service.GetSessionsAsync(groupId, year));
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<TrainingSession>> CreateSession(TrainingSessionUpsertDto dto)
    {
        try
        {
            var session = await _service.CreateSessionAsync(dto);
            return Created($"/api/training/sessions/{session.Id}", session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("sessions/{id:int}")]
    public async Task<ActionResult<TrainingSession>> UpdateSession(int id, TrainingSessionUpsertDto dto)
    {
        try
        {
            var session = await _service.UpdateSessionAsync(id, dto);
            return session is null ? NotFound() : Ok(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("sessions/{id:int}/sign")]
    public async Task<ActionResult<TrainingSession>> SignSession(int id, SignSessionDto dto)
    {
        var session = await _service.SignSessionAsync(id, dto);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("sessions/{id:int}/attendance")]
    public async Task<ActionResult<IEnumerable<AttendanceRecord>>> GetAttendance(int id)
    {
        return Ok(await _service.GetAttendanceAsync(id));
    }

    [HttpPut("sessions/{id:int}/attendance")]
    public async Task<ActionResult<AttendanceRecord>> UpsertAttendance(int id, AttendanceUpsertDto dto)
    {
        try
        {
            var record = await _service.UpsertAttendanceAsync(id, dto);
            return record is null ? NotFound() : Ok(record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("lms-results")]
    public async Task<ActionResult<AttendanceRecord>> ApplyLmsResult(LmsResultDto dto)
    {
        try
        {
            var record = await _service.ApplyLmsResultAsync(dto);
            return record is null ? NotFound() : Ok(record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TrainingSummaryDto>> GetSummary(
        [FromQuery] int groupId,
        [FromQuery] int year,
        [FromQuery] DateOnly? referenceDate)
    {
        var summary = await _service.GetSummaryAsync(
            groupId,
            year,
            referenceDate ?? Clock.Today);

        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("protocol")]
    public async Task<ActionResult<TrainingProtocolDto>> GetProtocol([FromQuery] int groupId, [FromQuery] int year)
    {
        var protocol = await _service.GetProtocolAsync(groupId, year);
        return protocol is null ? NotFound() : Ok(protocol);
    }

    [HttpGet("reminders")]
    public async Task<ActionResult<IEnumerable<TrainingReminderDto>>> GetReminders([FromQuery] DateOnly? referenceDate)
    {
        return Ok(await _service.GetRemindersAsync(referenceDate ?? Clock.Today));
    }

    [HttpPost("sessions/{id:int}/change-requests")]
    public async Task<ActionResult<TrainingChangeRequest>> CreateChangeRequest(int id, CreateChangeRequestDto dto)
    {
        try
        {
            var request = await _service.CreateChangeRequestAsync(id, dto);
            return request is null ? NotFound() : Ok(request);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("change-requests/{id:int}/resolve")]
    public async Task<ActionResult<TrainingChangeRequest>> ResolveChangeRequest(int id, ResolveChangeRequestDto dto)
    {
        try
        {
            var request = await _service.ResolveChangeRequestAsync(id, dto);
            return request is null ? NotFound() : Ok(request);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("sessions/{id:int}/audit")]
    public async Task<ActionResult<IEnumerable<TrainingAuditLog>>> GetAudit(int id)
    {
        return Ok(await _service.GetAuditAsync(id));
    }
}

