using System.Text.Json;
using Gochs.Modules.Siz;
namespace Gochs.Modules.Operations;
public record GenerateOrder(string Number,string Basis);
public record AlertRequest(string Message);
public record AutoEvacuation(int? Shift);
public record EquipmentAssignment(int SizCardId,int? NfgoUnitId);
public static class OperationsApi
{
    public static void Map(WebApplication app)
    {
        Catalog.Map<Formation>(app,"/api/nfgo/formations");
        Catalog.Map<NfgoUnit>(app,"/api/nfgo/units",async(db,u,create)=>{await Rules.Find<Formation>(db,u.FormationId);Rules.Require(await db.Employees.CountAsync(x=>x.NfgoUnitId==u.Id&&x.PersonnelStatus!=PersonnelStatus.Dismissed)<=u.Capacity,"Штат меньше назначенного состава.");});
        Catalog.Map<EquipmentNorm>(app,"/api/nfgo/norms");
        app.MapPost("/api/nfgo/equipment",async(EquipmentAssignment dto,AppDbContext db)=>{
            var card=await Rules.Find<SizCard>(db,dto.SizCardId);
            Rules.Require(card.Status=="InStock","Назначать резерв звену можно только со склада.");
            if(dto.NfgoUnitId.HasValue)await Rules.Find<NfgoUnit>(db,dto.NfgoUnitId.Value);
            card.NfgoUnitId=dto.NfgoUnitId;Rules.Audit(db,"Назначено имущество НФГО",dto);await db.SaveChangesAsync();return Results.Ok(card);
        });
        Catalog.Map<ProtectiveStructure>(app,"/api/protective-structures",async(db,p,create)=>{var assigned=await db.Employees.CountAsync(x=>x.ProtectiveStructureId==p.Id);Rules.Require(assigned<=p.Capacity,"Вместимость меньше числа распределенных сотрудников.");Rules.Require(assigned==0||p.Condition!="Unfit","Сначала перераспределите сотрудников из непригодного сооружения.");});
        Catalog.Map<Vehicle>(app,"/api/evacuation/vehicles",async(db,v,create)=>{var n=await db.Employees.CountAsync(x=>x.VehicleId==v.Id);Rules.Require(n<=v.Capacity,"Вместимость меньше числа назначенных пассажиров.");Rules.Require(n==0||v.Available,"Сначала перераспределите пассажиров недоступного транспорта.");});
        Catalog.Map<ReceptionPoint>(app,"/api/evacuation/points",async(db,p,create)=>Rules.Require(await db.Employees.CountAsync(x=>x.ReceptionPointId==p.Id)<=p.Capacity,"Вместимость меньше числа назначенных сотрудников."));
        Catalog.Map<EvacuationRoute>(app,"/api/evacuation/routes");
        Catalog.Map<Activity>(app,"/api/activities");
        app.MapGet("/api/protective-structures/inspections",async(AppDbContext db)=>await db.Inspections.AsNoTracking().ToListAsync());
        app.MapPost("/api/protective-structures/inspections",async(Inspection input,AppDbContext db)=>{
            Rules.Validate(input);var p=await Rules.Find<ProtectiveStructure>(db,input.ProtectiveStructureId);
            Rules.Require(input.NextInspectionDate>input.Date&&input.Date<=Clock.Today,"Проверьте даты проверки.");
            Rules.Require(input.Condition!="Ready"||(input.VentilationWorks&&input.DoorsSealed),"Нельзя признать сооружение готовым при неисправных ФВУ или дверях.");
            Rules.Require(input.Condition!="Unfit"||!await db.Employees.AnyAsync(x=>x.ProtectiveStructureId==p.Id),"Перераспределите сотрудников перед признанием сооружения непригодным.");
            p.Condition=input.Condition;p.NextInspectionDate=input.NextInspectionDate;input.Id=0;db.Add(input);Rules.Audit(db,"Проверка сооружения",input);await db.SaveChangesAsync();return Results.Created("/api/protective-structures/inspections",input);
        });
        app.MapGet("/api/evacuation/summary",Summary);
        app.MapPost("/api/evacuation/auto-plan",async(AutoEvacuation dto,AppDbContext db)=>{
            var all=await db.Employees.ToListAsync();var candidates=all.Where(x=>x.IsSubjectToEvacuation&&x.PersonnelStatus!=PersonnelStatus.Dismissed&&(dto.Shift==null||x.Shift==dto.Shift)).OrderByDescending(x=>x.NeedsAssistance).ThenBy(x=>x.Department).ToList();
            var vehicles=(await db.Vehicles.ToListAsync()).Where(x=>x.Available).OrderBy(x=>x.Contracted).ThenByDescending(x=>x.Capacity).ToList();var points=await db.ReceptionPoints.OrderBy(x=>x.Id).ToListAsync();
            var selected=candidates.Select(x=>x.Id).ToHashSet();
            foreach(var e in candidates){e.VehicleId=null;e.ReceptionPointId=null;}
            foreach(var e in candidates)
            {
                var v=vehicles.FirstOrDefault(v=>all.Count(x=>x.VehicleId==v.Id)<v.Capacity);
                var p=points.FirstOrDefault(p=>all.Count(x=>x.ReceptionPointId==p.Id)<p.Capacity);
                e.VehicleId=v?.Id;e.ReceptionPointId=p?.Id;
            }
            Rules.Audit(db,"Автоматическое распределение эвакуации",new{dto.Shift,Count=candidates.Count});await db.SaveChangesAsync();return await Summary(db);
        });
        app.MapPost("/api/evacuation/reset",async(AppDbContext db)=>{foreach(var e in await db.Employees.ToListAsync()){e.VehicleId=null;e.ReceptionPointId=null;}await db.SaveChangesAsync();return Results.NoContent();});
        app.MapPost("/api/nfgo/units/{id:int}/assemble",async(int id,AlertRequest dto,AppDbContext db)=>{
            await Rules.Find<NfgoUnit>(db,id);var text=Rules.Text(dto.Message,"Сообщение");var people=await db.Employees.Where(x=>x.NfgoUnitId==id&&x.PersonnelStatus!=PersonnelStatus.Dismissed).ToListAsync();
            foreach(var e in people)db.Notifications.Add(new(){EmployeeId=e.Id,Message=text});Rules.Audit(db,"Учебный сбор формирования",new{id,Count=people.Count});await db.SaveChangesAsync();return Results.Ok(new{count=people.Count,channel="Внутренние уведомления"});
        });
        app.MapGet("/api/nfgo/orders",async(AppDbContext db)=>await db.NfgoOrders.AsNoTracking().ToListAsync());
        app.MapPost("/api/nfgo/orders",async(GenerateOrder dto,AppDbContext db)=>{
            var snapshot=await Snapshot(db);var org=await db.Organizations.SingleAsync();
            var order=new NfgoOrder{Number=Rules.Text(dto.Number,"Номер"),Basis=Rules.Text(dto.Basis,"Основание"),Organization=org.Name,Snapshot=snapshot};db.Add(order);Rules.Audit(db,"Приказ НФГО создан",dto);await db.SaveChangesAsync();return Results.Created("/api/nfgo/orders",order);
        });
        app.MapPost("/api/nfgo/orders/{id:int}/refresh",async(int id,AppDbContext db)=>{var o=await Rules.Find<NfgoOrder>(db,id);Rules.Require(o.Status=="Draft","Подписанный приказ неизменяем.");o.Snapshot=await Snapshot(db);await db.SaveChangesAsync();return Results.Ok(o);});
        app.MapPost("/api/nfgo/orders/{id:int}/sign",async(int id,Signature dto,AppDbContext db)=>{
            var o=await Rules.Find<NfgoOrder>(db,id);Rules.Require(o.Status=="Draft","Приказ уже подписан.");
            Rules.Require(o.Snapshot==await Snapshot(db),"Состав изменился. Обновите проект приказа перед подписанием.");o.Status="Signed";o.SignedBy=Rules.Text(dto.SignedBy,"Подписант");Rules.Audit(db,"Подписан приказ НФГО",new{o.Number,o.SignedBy});await db.SaveChangesAsync();return Results.Ok(o);
        });
        app.MapGet("/api/wartime/leaves",async(AppDbContext db)=>await db.Leaves.AsNoTracking().ToListAsync());
        app.MapPost("/api/wartime/leaves",async(Leave dto,AppDbContext db)=>{
            Rules.Validate(dto);await Rules.Find<Employee>(db,dto.EmployeeId);Rules.Require(dto.EndDate>=dto.StartDate,"Окончание отсутствия раньше начала.");
            Rules.Require(dto.Kind!="Vacation"||!await db.SystemOperationalStates.AnyAsync(x=>x.LeaveRestrictionsEnabled),"В специальном режиме создание отпусков запрещено.");
            Rules.Require(!await db.Leaves.AnyAsync(x=>x.EmployeeId==dto.EmployeeId&&x.Status=="Approved"&&x.StartDate<=dto.EndDate&&x.EndDate>=dto.StartDate),"Периоды отсутствия пересекаются.");
            dto.Id=0;dto.Status="Approved";db.Add(dto);Rules.Audit(db,"Оформлено отсутствие",dto);await db.SaveChangesAsync();return Results.Created("/api/wartime/leaves",dto);
        });
        app.MapPost("/api/wartime/leaves/{id:int}/cancel",async(int id,AppDbContext db)=>{var leave=await Rules.Find<Leave>(db,id);leave.Status="Cancelled";await db.SaveChangesAsync();return Results.Ok(leave);});
        app.MapGet("/api/wartime/schedules",async(AppDbContext db)=>await db.WorkSchedules.AsNoTracking().ToListAsync());
        app.MapGet("/api/wartime/protected-forms",async(AppDbContext db)=>{
            Rules.Require(await db.SystemOperationalStates.AnyAsync(x=>x.ProtectedFormsEnabled),"Формы доступны после активации приказа.");
            return Results.Ok(new{employees=await db.Employees.CountAsync(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed),issued=await db.SizCards.CountAsync(x=>x.Status=="Issued"),shelterAssigned=await db.Employees.CountAsync(x=>x.ProtectiveStructureId!=null),reserved=await db.Employees.CountAsync(x=>x.Reserved)});
        });
        app.MapGet("/api/notifications",async(AppDbContext db)=>await db.Notifications.AsNoTracking().OrderByDescending(x=>x.Id).ToListAsync());
        app.MapPost("/api/notifications/{id:int}/acknowledge",async(int id,AppDbContext db)=>{var n=await Rules.Find<Notification>(db,id);n.Acknowledged=true;await db.SaveChangesAsync();return Results.Ok(n);});
        MapAttachments(app);
    }
    public static async Task<string> Snapshot(AppDbContext db)
    {
        var people=await db.Employees.AsNoTracking().Where(x=>x.NfgoUnitId!=null).OrderBy(x=>x.Id).ToListAsync();Rules.Require(people.Count>0,"Нет назначений в НФГО.");
        Rules.Require(people.All(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed),"Есть уволенные сотрудники в составе НФГО.");
        var units=await db.NfgoUnits.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();var norms=await db.EquipmentNorms.AsNoTracking().OrderBy(x=>x.Id).ToListAsync();
        return JsonSerializer.Serialize(new{members=people.Select(x=>new{x.Id,x.PersonnelNumber,x.FullName,x.Position,x.NfgoUnitId,x.NfgoRole}),units,norms});
    }
    public static async Task<object> Summary(AppDbContext db)
    {
        var people=await db.Employees.AsNoTracking().Where(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed).ToListAsync();var subject=people.Where(x=>x.IsSubjectToEvacuation).ToList();var vehicles=(await db.Vehicles.AsNoTracking().ToListAsync()).Where(x=>x.Available).ToList();var points=await db.ReceptionPoints.AsNoTracking().ToListAsync();var seats=vehicles.Sum(x=>x.Capacity);
        return new{total=people.Count,subject=subject.Count,assigned=subject.Count(x=>x.VehicleId!=null&&x.ReceptionPointId!=null),seats,deficit=Math.Max(0,subject.Count-seats),waves=seats==0?(int?)null:(int)Math.Ceiling(subject.Count/(double)seats),placement=points.Sum(x=>x.Capacity),shifts=subject.GroupBy(x=>x.Shift).Select(g=>new{shift=g.Key,count=g.Count()}),unassigned=subject.Where(x=>x.VehicleId==null||x.ReceptionPointId==null).Select(x=>new{x.Id,x.FullName})};
    }
    private static void MapAttachments(WebApplication app)
    {
        app.MapGet("/api/attachments",async(string resource,int resourceId,AppDbContext db)=>await db.Attachments.AsNoTracking().Where(x=>x.Resource==resource&&x.ResourceId==resourceId).Select(x=>new{x.Id,x.Name,x.ContentType,x.Resource,x.ResourceId,size=x.Content.Length}).ToListAsync());
        app.MapPost("/api/attachments/{resource}/{id:int}",async(string resource,int id,HttpRequest req,AppDbContext db)=>{
            switch(resource){case "batch":await Rules.Find<SizBatch>(db,id);break;case "route":await Rules.Find<EvacuationRoute>(db,id);break;case "writeoff":await Rules.Find<WriteOffAct>(db,id);break;case "statement":await Rules.Find<IssueStatement>(db,id);break;case "disposal":await Rules.Find<Disposal>(db,id);break;default:throw new RuleException("Неизвестный тип вложения.",400);}
            var form=await req.ReadFormAsync();var file=form.Files.FirstOrDefault();Rules.Require(file!=null&&file.Length>0&&file.Length<=10*1024*1024,"Загрузите PDF, PNG или JPEG до 10 МБ.");
            var type=file!.ContentType;Rules.Require(type is "application/pdf" or "image/png" or "image/jpeg","Допустимы PDF, PNG и JPEG.");
            using var stream=new MemoryStream();await file.CopyToAsync(stream);var bytes=stream.ToArray();
            var name=Path.GetFileName(file.FileName.Replace('\\','/'));
            Rules.Require(name.Length<=180&&AttachmentFiles.Valid(name,type,bytes),"Расширение, тип или содержимое файла не соответствует PDF, PNG или JPEG.");
            var a=new Attachment{Resource=resource,ResourceId=id,Name=name,ContentType=type,Content=bytes};db.Add(a);await db.SaveChangesAsync();return Results.Created("/api/attachments/"+a.Id,new{a.Id,a.Name});
        });
        app.MapGet("/api/attachments/{id:int}",async(int id,AppDbContext db)=>{var a=await Rules.Find<Attachment>(db,id);return Results.File(a.Content,a.ContentType,a.Name);});
    }
}
