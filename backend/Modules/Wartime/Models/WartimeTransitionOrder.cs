using System.ComponentModel.DataAnnotations;

namespace Gochs.Modules.Wartime.Models;

public class WartimeTransitionOrder
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string OrganizationName { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    [Required, MaxLength(1000)]
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

    public WartimeOrderStatus Status { get; set; } = WartimeOrderStatus.Draft;

    [MaxLength(150)]
    public string? SignedBy { get; set; }

    public DateTime? SignedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WartimeActionLog> ActionLogs { get; set; } = new();
}
