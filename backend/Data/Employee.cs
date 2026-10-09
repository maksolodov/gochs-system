namespace Gochs.Modules.Training.Models;
public partial class Employee
{
    [Required, MaxLength(50)] public string PersonnelNumber { get; set; } = "";
    [Required, MaxLength(150)] public string Department { get; set; } = "";
    [Range(1,4)] public int GasMaskSize { get; set; } = 2;
    [Range(1,4)] public int SuitSize { get; set; } = 2;
    [Range(1,3)] public int Shift { get; set; } = 1;
    public bool IsSubjectToEvacuation { get; set; } = true;
    public bool NeedsAssistance { get; set; }
    public bool Reserved { get; set; }
    public bool UmcTrained { get; set; }
    public string Phone { get; set; } = "";
    public string NfgoRole { get; set; } = "Боец";
    public int? NfgoUnitId { get; set; }
    [JsonIgnore] public NfgoUnit? NfgoUnit { get; set; }
    public int? ProtectiveStructureId { get; set; }
    [JsonIgnore] public ProtectiveStructure? ProtectiveStructure { get; set; }
    public int? VehicleId { get; set; }
    [JsonIgnore] public Vehicle? Vehicle { get; set; }
    public int? ReceptionPointId { get; set; }
    [JsonIgnore] public ReceptionPoint? ReceptionPoint { get; set; }
}
