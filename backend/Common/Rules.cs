namespace Gochs.Common;
public static class Clock
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),Zone);
    public static DateOnly Today => DateOnly.FromDateTime(Local(DateTime.UtcNow));
    public static DateTime FromLocal(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),Zone);
}
public class RuleException(string message, int status = 409) : Exception(message) { public int Status { get; } = status; }
public static class Rules
{
    public static void Require(bool condition, string message) { if (!condition) throw new RuleException(message); }
    public static void Validate(object value)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), errors, true))
            throw new RuleException(string.Join("; ", errors.Select(x => x.ErrorMessage)),400);
        foreach (var p in value.GetType().GetProperties().Where(p => p.PropertyType.IsEnum))
            Require(Enum.IsDefined(p.PropertyType,p.GetValue(value)!),"Недопустимое значение: " + p.Name);
    }
    public static string Text(string? value, string field) { Require(!string.IsNullOrWhiteSpace(value),"Заполните поле: "+field); return value!.Trim(); }
    public static async Task<T> Find<T>(AppDbContext db, int id) where T:class => await db.Set<T>().FindAsync(id) ?? throw new RuleException("Запись не найдена.",404);
    public static void Audit(AppDbContext db, string action, object details) => db.AuditEvents.Add(new(){Action=action,Details=System.Text.Json.JsonSerializer.Serialize(details)});
    public static string Number(string prefix) => $"{prefix}-{Clock.Today.Year}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
}
