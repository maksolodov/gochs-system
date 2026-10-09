namespace Gochs.Data;
public partial class AppDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<SizType> SizTypes => Set<SizType>();
    public DbSet<SizBatch> SizBatches => Set<SizBatch>();
    public DbSet<SizCard> SizCards => Set<SizCard>();
    public DbSet<IssueStatement> IssueStatements => Set<IssueStatement>();
    public DbSet<IssueLine> IssueLines => Set<IssueLine>();
    public DbSet<WriteOffAct> WriteOffActs => Set<WriteOffAct>();
    public DbSet<WriteOffLine> WriteOffLines => Set<WriteOffLine>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Disposal> Disposals => Set<Disposal>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<NfgoUnit> NfgoUnits => Set<NfgoUnit>();
    public DbSet<EquipmentNorm> EquipmentNorms => Set<EquipmentNorm>();
    public DbSet<ProtectiveStructure> ProtectiveStructures => Set<ProtectiveStructure>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ReceptionPoint> ReceptionPoints => Set<ReceptionPoint>();
    public DbSet<EvacuationRoute> EvacuationRoutes => Set<EvacuationRoute>();
    public DbSet<NfgoOrder> NfgoOrders => Set<NfgoOrder>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    private static void ConfigureShared(ModelBuilder b)
    {
        b.Entity<Employee>().HasIndex(x=>x.PersonnelNumber).IsUnique();
        b.Entity<SizCard>().HasIndex(x=>x.InventoryNumber).IsUnique();
        b.Entity<SizBatch>().HasIndex(x=>new{x.SizTypeId,x.BatchNumber,x.Size}).IsUnique();
        b.Entity<ProtectiveStructure>().HasIndex(x=>x.RegistrationNumber).IsUnique();
        b.Entity<IssueStatement>().HasIndex(x=>x.Number).IsUnique();
        b.Entity<WriteOffAct>().HasIndex(x=>x.Number).IsUnique();
        b.Entity<NfgoOrder>().HasIndex(x=>x.Number).IsUnique();
        b.Entity<Disposal>().HasIndex(x=>x.WriteOffActId).IsUnique();
        b.Entity<ExternalIdentity>().HasIndex(x=>new{x.Source,x.SourceId}).IsUnique();
        b.Entity<EquipmentNorm>().HasIndex(x=>new{x.NfgoUnitId,x.SizTypeId}).IsUnique();
        b.Entity<IssueLine>().HasIndex(x=>new{x.IssueStatementId,x.SizCardId}).IsUnique();
        b.Entity<WriteOffLine>().HasIndex(x=>new{x.WriteOffActId,x.SizCardId}).IsUnique();
        b.Entity<SystemOperationalState>().HasOne<WartimeTransitionOrder>().WithMany().HasForeignKey(x=>x.ActiveOrderId).OnDelete(DeleteBehavior.Restrict);
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(x=>x.GetForeignKeys())) fk.DeleteBehavior=DeleteBehavior.Restrict;
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken=default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(x=>x.State is EntityState.Modified or EntityState.Deleted))
            if (entry.Entity is AuditEvent or TrainingAuditLog or WartimeActionLog)
                throw new RuleException("Историю действий нельзя изменять или удалять.");
        return base.SaveChangesAsync(cancellationToken);
    }
}
