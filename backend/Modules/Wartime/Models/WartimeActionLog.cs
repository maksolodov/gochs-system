using System.ComponentModel.DataAnnotations;

namespace Gochs.Modules.Wartime.Models;

public class WartimeActionLog
{
    public int Id { get; set; }

    public int WartimeTransitionOrderId { get; set; }

    public WartimeTransitionOrder? WartimeTransitionOrder { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
