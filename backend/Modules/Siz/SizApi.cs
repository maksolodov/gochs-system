namespace Gochs.Modules.Siz;
public static class SizApi
{
    public static void Map(WebApplication app)
    {
        Catalog.Map<Warehouse>(app,"/api/siz/warehouses");Catalog.Map<SizType>(app,"/api/siz/types");Catalog.Map<Contractor>(app,"/api/siz/contractors");
        app.MapGet("/api/siz/batches",async(AppDbContext db)=>await db.SizBatches.AsNoTracking().ToListAsync());
        app.MapPost("/api/siz/batches",async(BatchReceipt dto,SizService service)=>Results.Created("/api/siz/batches",await service.Receive(dto)));
        app.MapPut("/api/siz/batches/{id:int}",async(int id,SizBatch input,AppDbContext db)=>{
            Rules.Validate(input);var b=await Rules.Find<SizBatch>(db,id);
            Rules.Require(input.SizTypeId==b.SizTypeId&&input.Size==b.Size&&input.BatchNumber==b.BatchNumber,"Тип, размер и номер принятой партии неизменяемы.");
            Rules.Require(input.ExpiryDate>input.ManufactureDate,"Проверьте даты.");await Rules.Find<Warehouse>(db,input.WarehouseId);
            input.Id=id;db.Entry(b).CurrentValues.SetValues(input);Rules.Audit(db,"Изменена партия",input);await db.SaveChangesAsync();return Results.Ok(b);
        });
        app.MapPost("/api/siz/batches/bulk",async(BulkBatchUpdate dto,AppDbContext db)=>{
            await Rules.Find<Warehouse>(db,dto.WarehouseId);var batches=await db.SizBatches.Where(x=>dto.BatchIds.Contains(x.Id)).ToListAsync();
            Rules.Require(batches.Count==dto.BatchIds.Distinct().Count(),"Часть партий не найдена.");
            foreach(var b in batches){b.WarehouseId=dto.WarehouseId;b.Shelf=dto.Shelf;b.LastInspectionDate=dto.LastInspectionDate;b.InspectionNumber=dto.InspectionNumber;}
            Rules.Audit(db,"Массовое изменение партий",dto);await db.SaveChangesAsync();return Results.Ok(batches);
        });
        app.MapGet("/api/siz/cards",async(AppDbContext db)=>await db.SizCards.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
        app.MapGet("/api/siz/cards/{id:int}",async(int id,AppDbContext db)=>await Rules.Find<SizCard>(db,id));
        app.MapGet("/api/siz/cards/{id:int}/barcode",async(int id,AppDbContext db)=>{await Rules.Find<SizCard>(db,id);return Results.Content(Barcode.Svg(id),"image/svg+xml");});
        app.MapPut("/api/siz/cards/{id:int}/condition",async(int id,CardCondition dto,AppDbContext db)=>{
            var c=await Rules.Find<SizCard>(db,id);Rules.Require(c.Status is "InStock" or "Issued" or "Inspection","Нельзя менять списанное изделие.");Rules.Require(dto.Condition is "Fit" or "Unfit" or "Inspection","Неизвестное состояние.");
            c.Condition=dto.Condition;if(c.Status!="Issued")c.Status=dto.Condition=="Inspection"?"Inspection":"InStock";
            Rules.Audit(db,"Состояние СИЗ",new{id,dto.Condition});await db.SaveChangesAsync();return Results.Ok(c);
        });
        app.MapGet("/api/siz/alerts",async(AppDbContext db)=>await db.SizCards.AsNoTracking().Where(x=>x.Status!="Disposed"&&x.Status!="WrittenOff"&&x.SizBatch!.ExpiryDate<=Clock.Today.AddMonths(6)).ToListAsync());
        app.MapGet("/api/siz/statements",async(AppDbContext db)=>await db.IssueStatements.AsNoTracking().Include(x=>x.Lines).ToListAsync());
        app.MapPost("/api/siz/statements",async(IssueRequest dto,SizService s)=>Results.Created("/api/siz/statements",await s.IssueDraft(dto)));
        app.MapPut("/api/siz/statements/{id:int}",async(int id,IssueRequest dto,SizService s)=>await s.IssueDraft(dto,id));
        app.MapPost("/api/siz/statements/auto",async(AutoIssueRequest dto,SizService s)=>Results.Created("/api/siz/statements",await s.AutoIssue(dto)));
        app.MapPost("/api/siz/statements/{id:int}/confirm",async(int id,Signature dto,SizService s)=>await s.Confirm(id,dto));
        app.MapDelete("/api/siz/statements/{id:int}",async(int id,AppDbContext db)=>{var s=await db.IssueStatements.Include(x=>x.Lines).SingleAsync(x=>x.Id==id);Rules.Require(s.Status=="Draft","Нельзя удалить подтвержденную ведомость.");db.IssueLines.RemoveRange(s.Lines);db.Remove(s);await db.SaveChangesAsync();return Results.NoContent();});
        app.MapGet("/api/siz/writeoffs",async(AppDbContext db)=>await db.WriteOffActs.AsNoTracking().Include(x=>x.Lines).ToListAsync());
        app.MapGet("/api/siz/writeoffs/candidates",async(AppDbContext db)=>await db.SizCards.AsNoTracking().Where(x=>(x.Status=="InStock"||x.Status=="Issued"||x.Status=="Inspection")&&(x.SizBatch!.ExpiryDate<=Clock.Today||x.Condition=="Unfit")).ToListAsync());
        app.MapPost("/api/siz/writeoffs",async(WriteOffRequest dto,SizService s)=>Results.Created("/api/siz/writeoffs",await s.DraftWriteOff(dto)));
        app.MapPost("/api/siz/writeoffs/{id:int}/sign",async(int id,Signature dto,SizService s)=>await s.SignWriteOff(id,dto));
        app.MapGet("/api/siz/disposals",async(AppDbContext db)=>await db.Disposals.AsNoTracking().ToListAsync());
        app.MapPost("/api/siz/disposals",async(Disposal dto,SizService s)=>Results.Created("/api/siz/disposals",await s.Dispose(dto)));
    }
}
