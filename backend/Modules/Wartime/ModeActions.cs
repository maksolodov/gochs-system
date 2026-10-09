namespace Gochs.Modules.Wartime;
public static class ModeActions
{
    public static async Task Activate(AppDbContext db,WartimeTransitionOrder order)
    {
        var leaves=await db.Leaves.Where(x=>x.Kind=="Vacation"&&x.Status=="Approved"&&x.EndDate>=Clock.Today).ToListAsync();
        foreach(var leave in leaves)leave.Status="Suspended";
        var people=await db.Employees.Where(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed).ToListAsync();
        foreach(var e in people)
        {
            if(e.PersonnelStatus==PersonnelStatus.Vacation)e.PersonnelStatus=PersonnelStatus.Active;
            db.WorkSchedules.Add(new(){EmployeeId=e.Id,Mode="Wartime",Description=string.IsNullOrWhiteSpace(order.WorkScheduleDescription)?$"Смена {e.Shift}: {(e.Shift-1)*8:00}:00–{e.Shift*8%24:00}:00":order.WorkScheduleDescription});
            if(e.Reserved)db.Notifications.Add(new(){EmployeeId=e.Id,Message="Учебное оповещение. Код 111. Срочно прибыть на рабочее место."});
        }
        Rules.Audit(db,"Активация режима",new{order.OrderNumber,Employees=people.Count,SuspendedLeaves=leaves.Count});
    }
}
