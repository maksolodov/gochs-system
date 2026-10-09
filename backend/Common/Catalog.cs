namespace Gochs.Common;
public static class Catalog
{
    public static void Map<T>(WebApplication app,string path,Func<AppDbContext,T,bool,Task>? validate=null) where T:Entity,new()
    {
        var g=app.MapGroup(path).WithTags(typeof(T).Name);
        g.MapGet("",async(AppDbContext db)=>await db.Set<T>().AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
        g.MapGet("/{id:int}",async(int id,AppDbContext db)=>await Rules.Find<T>(db,id));
        g.MapPost("",async(T item,AppDbContext db)=>{
            item.Id=0;Rules.Validate(item);
            if(validate!=null)await validate(db,item,true);
            db.Add(item);Rules.Audit(db,"Создание "+typeof(T).Name,item);await db.SaveChangesAsync();
            return Results.Created(path+"/"+item.Id,item);
        });
        g.MapPut("/{id:int}",async(int id,T item,AppDbContext db)=>{
            item.Id=id;Rules.Validate(item);
            var old=await Rules.Find<T>(db,id);
            if(validate!=null)await validate(db,item,false);
            db.Entry(old).CurrentValues.SetValues(item);Rules.Audit(db,"Изменение "+typeof(T).Name,item);await db.SaveChangesAsync();return Results.Ok(old);
        });
        g.MapDelete("/{id:int}",async(int id,AppDbContext db)=>{
            var item=await Rules.Find<T>(db,id);db.Remove(item);Rules.Audit(db,"Удаление "+typeof(T).Name,new{id});await db.SaveChangesAsync();return Results.NoContent();
        });
    }
}
