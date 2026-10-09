namespace Gochs.Modules.Personnel;
public static class PersonnelApi
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/employees",async(AppDbContext db)=>await db.Employees.AsNoTracking().OrderBy(x=>x.FullName).ToListAsync());
        app.MapGet("/api/employees/{id:int}",async(int id,AppDbContext db)=>await Rules.Find<Employee>(db,id));
        app.MapPost("/api/employees",async(Employee employee,AppDbContext db)=>{
            employee.Id=0;await Validate(db,employee);db.Add(employee);Rules.Audit(db,"Создан сотрудник",employee);await db.SaveChangesAsync();return Results.Created("/api/employees/"+employee.Id,employee);
        });
        app.MapPut("/api/employees/{id:int}",async(int id,Employee employee,AppDbContext db)=>{
            var old=await Rules.Find<Employee>(db,id);employee.Id=id;await Validate(db,employee);
            Rules.Require(old.TrainingGroupId==employee.TrainingGroupId || !await db.AttendanceRecords.AnyAsync(x=>x.EmployeeId==id),"Нельзя менять учебную группу при наличии истории посещаемости.");
            db.Entry(old).CurrentValues.SetValues(employee);Rules.Audit(db,"Изменен сотрудник",employee);await db.SaveChangesAsync();return Results.Ok(old);
        });
        app.MapDelete("/api/employees/{id:int}",async(int id,AppDbContext db)=>{db.Remove(await Rules.Find<Employee>(db,id));Rules.Audit(db,"Удален сотрудник",new{id});await db.SaveChangesAsync();return Results.NoContent();});
        app.MapPost("/api/employees/import",async(ImportEmployee dto,AppDbContext db)=>{
            Rules.Text(dto.Source,"Источник");Rules.Text(dto.SourceId,"Исходный ID");Rules.Validate(dto.Employee);
            var map=await db.ExternalIdentities.SingleOrDefaultAsync(x=>x.Source==dto.Source&&x.SourceId==dto.SourceId);
            if(map!=null) return Results.Ok(await Rules.Find<Employee>(db,map.EmployeeId));
            var employee=await db.Employees.SingleOrDefaultAsync(x=>x.PersonnelNumber==dto.Employee.PersonnelNumber);
            if(employee==null){employee=dto.Employee;employee.Id=0;await Validate(db,employee);db.Add(employee);}
            db.ExternalIdentities.Add(new(){Source=dto.Source,SourceId=dto.SourceId,Employee=employee});await db.SaveChangesAsync();return Results.Ok(employee);
        });
    }
    public static async Task Validate(AppDbContext db,Employee e)
    {
        Rules.Validate(e);
        Rules.Require(!await db.Employees.AnyAsync(x=>x.PersonnelNumber==e.PersonnelNumber&&x.Id!=e.Id),"Табельный номер уже используется.");
        if(e.PersonnelStatus==PersonnelStatus.Vacation)Rules.Require(!await db.SystemOperationalStates.AnyAsync(x=>x.LeaveRestrictionsEnabled),"В специальном режиме оформление отпусков запрещено.");
        if(e.TrainingGroupId.HasValue)await Rules.Find<TrainingGroup>(db,e.TrainingGroupId.Value);
        if(e.NfgoUnitId.HasValue){var u=await Rules.Find<NfgoUnit>(db,e.NfgoUnitId.Value);Rules.Require(await db.Employees.CountAsync(x=>x.NfgoUnitId==u.Id&&x.Id!=e.Id&&x.PersonnelStatus!=PersonnelStatus.Dismissed)<u.Capacity,"Штат звена заполнен.");}
        if(e.ProtectiveStructureId.HasValue){var p=await Rules.Find<ProtectiveStructure>(db,e.ProtectiveStructureId.Value);Rules.Require(p.Condition!="Unfit","Сооружение непригодно.");Rules.Require(await db.Employees.CountAsync(x=>x.ProtectiveStructureId==p.Id&&x.Id!=e.Id)<p.Capacity,"Вместимость сооружения исчерпана.");}
        if(e.VehicleId.HasValue){var v=await Rules.Find<Vehicle>(db,e.VehicleId.Value);Rules.Require(v.Available,"Транспорт недоступен.");Rules.Require(e.IsSubjectToEvacuation,"Сотрудник не подлежит эвакуации.");Rules.Require(await db.Employees.CountAsync(x=>x.VehicleId==v.Id&&x.Id!=e.Id)<v.Capacity,"Нет мест в транспорте.");}
        if(e.ReceptionPointId.HasValue){var p=await Rules.Find<ReceptionPoint>(db,e.ReceptionPointId.Value);Rules.Require(await db.Employees.CountAsync(x=>x.ReceptionPointId==p.Id&&x.Id!=e.Id)<p.Capacity,"Нет мест в пункте приема.");}
    }
}
public record ImportEmployee(string Source,string SourceId,Employee Employee);
