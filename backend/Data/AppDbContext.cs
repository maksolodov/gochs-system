using Microsoft.EntityFrameworkCore;

using Gochs.Modules.Training.Models;
using Gochs.Modules.Wartime.Models;

namespace Gochs.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }


    public DbSet<TrainingGroup> TrainingGroups => Set<TrainingGroup>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<TrainingChangeRequest> TrainingChangeRequests => Set<TrainingChangeRequest>();
    public DbSet<TrainingAuditLog> TrainingAuditLogs => Set<TrainingAuditLog>();
    public DbSet<WartimeTransitionOrder> WartimeTransitionOrders => Set<WartimeTransitionOrder>();
    public DbSet<SystemOperationalState> SystemOperationalStates => Set<SystemOperationalState>();
    public DbSet<WartimeActionLog> WartimeActionLogs => Set<WartimeActionLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureShared(modelBuilder);

        modelBuilder.Entity<TrainingGroup>()
            .HasIndex(x => new { x.Year, x.Name })
            .IsUnique();

        modelBuilder.Entity<Employee>()
            .HasOne(x => x.TrainingGroup)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.TrainingGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingSession>()
            .HasOne(x => x.TrainingGroup)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.TrainingGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
            .HasIndex(x => new { x.TrainingSessionId, x.EmployeeId })
            .IsUnique();

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(x => x.TrainingSession)
            .WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(x => x.Employee)
            .WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingChangeRequest>()
            .HasOne(x => x.TrainingSession)
            .WithMany(x => x.ChangeRequests)
            .HasForeignKey(x => x.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WartimeTransitionOrder>()
            .HasIndex(x => x.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<WartimeActionLog>()
            .HasOne(x => x.WartimeTransitionOrder)
            .WithMany(x => x.ActionLogs)
            .HasForeignKey(x => x.WartimeTransitionOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime,DateTime>(v=>v, v=>DateTime.SpecifyKind(v,DateTimeKind.Utc));
