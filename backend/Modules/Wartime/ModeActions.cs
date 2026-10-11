using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Gochs.Modules.Training.Models;

namespace Gochs.Modules.Wartime;

public record WartimeReturnResult(string Message,int RestoredLeaves,int RestoredEmployees,int PreservedLeaves,int PreservedEmployees);

public record WartimeRecordSnapshot(int Id,string AfterHash);
public record WartimeActivationSnapshot(int OrderId,List<WartimeRecordSnapshot> Leaves,List<WartimeRecordSnapshot> Employees);

public static class ModeActions
{
    public static async Task Activate(AppDbContext db,WartimeTransitionOrder order)
    {
        var leaves=await db.Leaves.Where(x=>x.Kind=="Vacation"&&x.Status=="Approved"&&x.EndDate>=Clock.Today).ToListAsync();
        var people=await db.Employees.Where(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed).ToListAsync();
        var changedPeople=people.Where(x=>x.PersonnelStatus==PersonnelStatus.Vacation).ToList();
        foreach(var leave in leaves)leave.Status="Suspended";
        foreach(var e in people)
        {
            if(e.PersonnelStatus==PersonnelStatus.Vacation)e.PersonnelStatus=PersonnelStatus.Active;
            db.WorkSchedules.Add(new(){EmployeeId=e.Id,Mode="Wartime",Description=string.IsNullOrWhiteSpace(order.WorkScheduleDescription)?$"Смена {e.Shift}: {(e.Shift-1)*8:00}:00–{e.Shift*8%24:00}:00":order.WorkScheduleDescription});
            if(e.Reserved)db.Notifications.Add(new(){EmployeeId=e.Id,Message="Учебное оповещение. Код 111. Срочно прибыть на рабочее место."});
        }
        db.AuditEvents.Add(new(){Action="WartimeActivationSnapshot:"+order.Id,Details=JsonSerializer.Serialize(new WartimeActivationSnapshot(order.Id,leaves.Select(x=>new WartimeRecordSnapshot(x.Id,Fingerprint(x))).ToList(),changedPeople.Select(x=>new WartimeRecordSnapshot(x.Id,Fingerprint(x))).ToList()))});
        Rules.Audit(db,"Активация режима",new{order.OrderNumber,Employees=people.Count,SuspendedLeaves=leaves.Count});
    }

    public static async Task<WartimeReturnResult> ReturnToNormal(AppDbContext db,int? orderId)
    {
        var snapshotJson=orderId.HasValue?await db.AuditEvents.AsNoTracking().Where(x=>x.Action=="WartimeActivationSnapshot:"+orderId.Value).OrderByDescending(x=>x.Id).Select(x=>x.Details).FirstOrDefaultAsync():null;
        WartimeActivationSnapshot? snapshot=null;
        if(snapshotJson!=null)
        {
            try{snapshot=JsonSerializer.Deserialize<WartimeActivationSnapshot>(snapshotJson);}
            catch(JsonException){throw new InvalidOperationException("Снимок состояния поврежден. Возврат остановлен без изменения данных.");}
            if(snapshot?.OrderId!=orderId)throw new InvalidOperationException("Снимок состояния не соответствует приказу. Возврат остановлен без изменения данных.");
        }
        var restoredLeaves=0;var restoredEmployees=0;var preservedLeaves=0;var preservedEmployees=0;
        if(snapshot!=null)
        {
            var leaveIds=snapshot.Leaves.Select(x=>x.Id).ToList();
            var leaveSnapshots=snapshot.Leaves.ToDictionary(x=>x.Id,x=>x.AfterHash);
            foreach(var leave in await db.Leaves.Where(x=>leaveIds.Contains(x.Id)).ToListAsync())
            {
                if(leave.Status=="Suspended"&&Fingerprint(leave)==leaveSnapshots[leave.Id]){leave.Status="Approved";restoredLeaves++;}
                else preservedLeaves++;
            }
            var employeeIds=snapshot.Employees.Select(x=>x.Id).ToList();
            var employeeSnapshots=snapshot.Employees.ToDictionary(x=>x.Id,x=>x.AfterHash);
            foreach(var employee in await db.Employees.Where(x=>employeeIds.Contains(x.Id)).ToListAsync())
            {
                var activeLeave=await db.Leaves.AnyAsync(x=>x.EmployeeId==employee.Id&&x.Kind=="Vacation"&&x.Status=="Approved"&&x.StartDate<=Clock.Today&&x.EndDate>=Clock.Today);
                if(employee.PersonnelStatus==PersonnelStatus.Active&&activeLeave&&Fingerprint(employee)==employeeSnapshots[employee.Id]){employee.PersonnelStatus=PersonnelStatus.Vacation;restoredEmployees++;}
                else preservedEmployees++;
            }
        }
        var message=snapshot==null
            ?"Обычный режим включен. Исходные кадровые статусы и отпуска прежней активации неизвестны и сохранены без изменений. Специальные графики и уведомления сохранены."
            :$"Обычный режим включен. Восстановлено отпусков: {restoredLeaves}, статусов сотрудников: {restoredEmployees}. Не изменено записей с последующими изменениями или без актуального отпуска: {preservedLeaves+preservedEmployees}. Специальные графики и уведомления сохранены.";
        return new WartimeReturnResult(message,restoredLeaves,restoredEmployees,preservedLeaves,preservedEmployees);
    }

    private static string Fingerprint(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
