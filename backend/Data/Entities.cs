namespace Gochs.Data;

public abstract class Entity { public int Id { get; set; } }
public class Organization : Entity
{
    [Required] public string Name { get; set; } = "Учебный музей «Северный»";
    [Required] public string Category { get; set; } = "II";
    [Required] public string Head { get; set; } = "Орлов Алексей Викторович";
}
public class Warehouse : Entity { [Required] public string Name { get; set; } = ""; [Required] public string Address { get; set; } = ""; }
public class SizType : Entity
{
    [Required] public string Name { get; set; } = "";
    [Required, RegularExpression("GasMask|Suit|Medical|Other")] public string Category { get; set; } = "Other";
    [Range(0,10000000)] public decimal UnitPrice { get; set; }
    [Range(1,365)] public int DeliveryDays { get; set; } = 30;
    [Range(1,50)] public int ShelfLifeYears { get; set; } = 5;
    [Range(0,20)] public int PerEmployee { get; set; } = 1;
    [Required] public string WasteClass { get; set; } = "ТКО";
}
public class SizBatch : Entity
{
    public int SizTypeId { get; set; }
    [JsonIgnore] public SizType? SizType { get; set; }
    public int WarehouseId { get; set; }
    [JsonIgnore] public Warehouse? Warehouse { get; set; }
    [Required] public string BatchNumber { get; set; } = "";
    [Required] public string Manufacturer { get; set; } = "";
    public string Size { get; set; } = "";
    public string Shelf { get; set; } = "";
    public DateOnly ManufactureDate { get; set; }
    public DateOnly ReceivedDate { get; set; } = Clock.Today;
    public DateOnly ExpiryDate { get; set; }
    public DateOnly? LastInspectionDate { get; set; }
    public string InspectionNumber { get; set; } = "";
    public string Certificate { get; set; } = "";
}
public class SizCard : Entity
{
    public int SizBatchId { get; set; }
    [JsonIgnore] public SizBatch? SizBatch { get; set; }
    [Required] public string InventoryNumber { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Status { get; set; } = "InStock";
    public string Condition { get; set; } = "Fit";
    public int? EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public int? NfgoUnitId { get; set; }
    [JsonIgnore] public NfgoUnit? NfgoUnit { get; set; }
    public DateOnly? IssueDate { get; set; }
}
public class IssueStatement : Entity
{
    public string Number { get; set; } = "";
    [Required] public string Department { get; set; } = "";
    [Required] public string Basis { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public DateOnly Date { get; set; } = Clock.Today;
    public string? SignedBy { get; set; }
    public List<IssueLine> Lines { get; set; } = [];
}
public class IssueLine : Entity
{
    public int IssueStatementId { get; set; }
    [JsonIgnore] public IssueStatement? IssueStatement { get; set; }
    public int EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public int SizCardId { get; set; }
    [JsonIgnore] public SizCard? SizCard { get; set; }
    public string EmployeeName { get; set; } = "";
    public string PersonnelNumber { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public string Size { get; set; } = "";
}
public class WriteOffAct : Entity
{
    public string Number { get; set; } = "";
    [Required] public string Commission { get; set; } = "";
    [Required] public string Reason { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public DateOnly Date { get; set; } = Clock.Today;
    public string? SignedBy { get; set; }
    public List<WriteOffLine> Lines { get; set; } = [];
}
public class WriteOffLine : Entity
{
    public int WriteOffActId { get; set; }
    [JsonIgnore] public WriteOffAct? WriteOffAct { get; set; }
    public int SizCardId { get; set; }
    [JsonIgnore] public SizCard? SizCard { get; set; }
}
public class Contractor : Entity
{
    [Required] public string Name { get; set; } = "";
    [Required] public string License { get; set; } = "";
    public DateOnly ValidUntil { get; set; }
    [Required] public string WasteClasses { get; set; } = "ТКО;III-IV;Г";
}
public class Disposal : Entity
{
    [MaxLength(500)] public string Method { get; set; } = "";
    public int WriteOffActId { get; set; }
    [JsonIgnore] public WriteOffAct? WriteOffAct { get; set; }
    public int ContractorId { get; set; }
    [JsonIgnore] public Contractor? Contractor { get; set; }
    [Required] public string ContractNumber { get; set; } = "";
    [Required] public string TransferDocument { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Completed";
}
public class Formation : Entity
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Type { get; set; } = "";
    [Required] public string MchsCode { get; set; } = "";
    public string Location { get; set; } = "";
    public string Purpose { get; set; } = "";
}
public class NfgoUnit : Entity
{
    public int FormationId { get; set; }
    [JsonIgnore] public Formation? Formation { get; set; }
    [Required] public string Name { get; set; } = "";
    [Range(1,10000)] public int Capacity { get; set; } = 3;
    public string Location { get; set; } = "";
}
public class EquipmentNorm : Entity
{
    public int NfgoUnitId { get; set; }
    [JsonIgnore] public NfgoUnit? NfgoUnit { get; set; }
    public int SizTypeId { get; set; }
    [JsonIgnore] public SizType? SizType { get; set; }
    [Range(1,10000)] public int Quantity { get; set; }
}
public class ProtectiveStructure : Entity
{
    [Required] public string RegistrationNumber { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    [Required] public string ProtectionClass { get; set; } = "";
    [Required] public string Address { get; set; } = "";
    [Range(1,100000)] public int Capacity { get; set; }
    [RegularExpression("Ready|Limited|Unfit")] public string Condition { get; set; } = "Ready";
    public string ResponsiblePerson { get; set; } = "";
    public DateOnly? NextInspectionDate { get; set; }
}
public class Inspection : Entity
{
    public int ProtectiveStructureId { get; set; }
    [JsonIgnore] public ProtectiveStructure? ProtectiveStructure { get; set; }
    public DateOnly Date { get; set; }
    [Required] public string Inspector { get; set; } = "";
    [RegularExpression("Ready|Limited|Unfit")] public string Condition { get; set; } = "Ready";
    public bool VentilationWorks { get; set; }
    public bool DoorsSealed { get; set; }
    public string Findings { get; set; } = "";
    public DateOnly NextInspectionDate { get; set; }
}
public class Vehicle : Entity
{
    [Required] public string Name { get; set; } = "";
    public string PlateNumber { get; set; } = "";
    [Range(1,1000)] public int Capacity { get; set; }
    public bool Contracted { get; set; }
    public DateOnly? ContractValidUntil { get; set; }
    public string ContractNumber { get; set; } = "";
    public string DriverName { get; set; } = "";
    public bool Ready { get; set; } = true;
    public bool Available => Ready && (!Contracted || (!string.IsNullOrWhiteSpace(ContractNumber) && (ContractValidUntil == null || ContractValidUntil >= Clock.Today)));
}
public class ReceptionPoint : Entity
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Address { get; set; } = "";
    [Range(1,100000)] public int Capacity { get; set; }
    public string ContactPerson { get; set; } = "";
    public string ContactPhone { get; set; } = "";
}
public class EvacuationRoute : Entity
{
    [Required] public string Name { get; set; } = "";
    public int ReceptionPointId { get; set; }
    [JsonIgnore] public ReceptionPoint? ReceptionPoint { get; set; }
    public bool Reserve { get; set; }
    public string StartPoint { get; set; } = "";
    [Range(0,100000)] public double DistanceKm { get; set; }
    [Range(1,10000)] public int TravelTimeMinutes { get; set; } = 30;
    public string Description { get; set; } = "";
}
public class NfgoOrder : Entity
{
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; } = Clock.Today;
    public string Organization { get; set; } = "";
    public string Basis { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string Snapshot { get; set; } = "";
    public string? SignedBy { get; set; }
}
public class Leave : Entity
{
    public int EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [RegularExpression("Vacation|SickLeave|Maternity")] public string Kind { get; set; } = "Vacation";
    public string Status { get; set; } = "Approved";
}
public class WorkSchedule : Entity
{
    public int EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public string Mode { get; set; } = "Normal";
    public string Description { get; set; } = "09:00–18:00";
}
public class Notification : Entity
{
    public int? EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public string Message { get; set; } = "";
    public string Channel { get; set; } = "InApp";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Acknowledged { get; set; }
}
public class Activity : Entity
{
    [Required] public string Direction { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public int EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
    public DateOnly Deadline { get; set; }
    [Range(0,100)] public int Progress { get; set; }
}
public class AuditEvent : Entity
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "Оператор учебного стенда";
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
}
public class ExternalIdentity : Entity
{
    [Required] public string Source { get; set; } = "";
    [Required] public string SourceId { get; set; } = "";
    public int EmployeeId { get; set; }
    [JsonIgnore] public Employee? Employee { get; set; }
}
public class Attachment : Entity
{
    public string Resource { get; set; } = "";
    public int ResourceId { get; set; }
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    [JsonIgnore] public byte[] Content { get; set; } = [];
}
