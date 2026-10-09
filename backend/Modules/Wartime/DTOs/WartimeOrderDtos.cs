using System.ComponentModel.DataAnnotations;
using Gochs.Modules.Wartime.Models;

namespace Gochs.Modules.Wartime.DTOs;

public class WartimeOrderUpsertDto
{
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string OrganizationName { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    [Required, MinLength(5), MaxLength(1000)]
    public string Basis { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string ResponsiblePerson { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ResponsiblePosition { get; set; }

    [MaxLength(1000)]
    public string? NotificationProcedure { get; set; }

    [MaxLength(1000)]
    public string? WorkScheduleDescription { get; set; }

    [MaxLength(2000)]
    public string? AdditionalInstructions { get; set; }

    [Required, MaxLength(150)]
    public string CreatedBy { get; set; } = string.Empty;
}

public class SignWartimeOrderDto
{
    [Required, MaxLength(150)]
    public string SignedBy { get; set; } = string.Empty;
}

public class WartimeOrderStateDto
{
    public OperationalMode Mode { get; set; }
    public int? ActiveOrderId { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public bool SpecialWorkScheduleEnabled { get; set; }
    public bool LeaveRestrictionsEnabled { get; set; }
    public bool EmergencyNotificationsEnabled { get; set; }
    public bool ProtectedFormsEnabled { get; set; }
}
