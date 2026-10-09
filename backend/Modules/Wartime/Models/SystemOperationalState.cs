namespace Gochs.Modules.Wartime.Models;

public class SystemOperationalState
{
    public int Id { get; set; } = 1;

    public OperationalMode Mode { get; set; } = OperationalMode.Normal;

    public int? ActiveOrderId { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public bool SpecialWorkScheduleEnabled { get; set; }

    public bool LeaveRestrictionsEnabled { get; set; }

    public bool EmergencyNotificationsEnabled { get; set; }

    public bool ProtectedFormsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
