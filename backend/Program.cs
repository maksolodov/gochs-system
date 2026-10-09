using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;
using Gochs.Modules.Training.Services;
using Gochs.Modules.Wartime.Services;
using Gochs.Modules.Personnel;
using Gochs.Modules.Siz;
using Gochs.Modules.Operations;
using Gochs.Modules.Reports;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder=WebApplication.CreateBuilder(args);
var dataPath=Path.GetFullPath(builder.Configuration["DataPath"]??Path.Combine(builder.Environment.ContentRootPath,"..","data"));
Directory.CreateDirectory(dataPath);
var dbPath=Path.Combine(dataPath,"gochs.db");
if(builder.Configuration["backup"] is string backup){SchemaUpgrade.Backup(dbPath,backup);Console.WriteLine("Резервная копия создана.");return;}
using var instanceLock=new FileStream(Path.Combine(dataPath,"instance.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
if(builder.Configuration["restore"] is string restore){SchemaUpgrade.Restore(restore,dbPath);Console.WriteLine("База восстановлена.");return;}
var serverMode=builder.Configuration.GetValue<bool>("ServerMode");
var hosts=builder.Configuration["AllowedHosts"];
if(string.IsNullOrWhiteSpace(hosts)||hosts.Split(';').Any(h=>h.Contains('*')))throw new InvalidOperationException("Задайте точные имена AllowedHosts.");
var proxy=builder.Configuration["TrustedProxy"];
if(serverMode&&string.IsNullOrWhiteSpace(proxy))throw new InvalidOperationException("Для серверного режима задайте TrustedProxy.");
if(!string.IsNullOrWhiteSpace(proxy))builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o=>{
    o.ForwardedHeaders=Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor|Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.ForwardLimit=1;o.KnownNetworks.Clear();o.KnownProxies.Clear();o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});
var operatorPassword=Access.PreparePassword(dataPath,builder.Configuration);
var sessionLifetime=TimeSpan.FromMinutes(Math.Clamp(builder.Configuration.GetValue<int?>("SessionMinutes")??480,1,480));
builder.Services.AddSingleton<OperatorSessions>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
    o.Cookie.Name="Gochs.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
    o.Cookie.SecurePolicy=serverMode?CookieSecurePolicy.Always:CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan=sessionLifetime;o.SlidingExpiration=false;
    o.Events.OnRedirectToLogin=ctx=>{ctx.Response.StatusCode=401;return ctx.Response.WriteAsJsonAsync(new{message="Войдите как оператор."});};
    o.Events.OnRedirectToAccessDenied=ctx=>{ctx.Response.StatusCode=403;return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=ctx=>{if(!ctx.HttpContext.RequestServices.GetRequiredService<OperatorSessions>().Valid(ctx.Principal?.FindFirst("sid")?.Value))ctx.RejectPrincipal();return Task.CompletedTask;};
});
builder.Services.AddAuthorization(o=>o.FallbackPolicy=new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="Gochs.Csrf";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=serverMode?CookieSecurePolicy.Always:CookieSecurePolicy.SameAsRequest;});
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath,"keys"))).SetApplicationName("Gochs");
builder.WebHost.ConfigureKestrel(o=>{o.Limits.MaxRequestBodySize=12*1024*1024;o.AddServerHeader=false;});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o=>o.MultipartBodyLengthLimit=11*1024*1024);
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=dbPath,ForeignKeys=true,DefaultTimeout=30}.ToString()));
builder.Services.AddControllers().AddJsonOptions(o=>{o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues:false));o.JsonSerializerOptions.Converters.Add(new OffsetTimeConverter());o.JsonSerializerOptions.ReferenceHandler=ReferenceHandler.IgnoreCycles;});
builder.Services.ConfigureHttpJsonOptions(o=>{o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues:false));o.SerializerOptions.Converters.Add(new OffsetTimeConverter());o.SerializerOptions.ReferenceHandler=ReferenceHandler.IgnoreCycles;});
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ITrainingService,TrainingService>();
builder.Services.AddScoped<IWartimeOrderService,WartimeOrderService>();
builder.Services.AddScoped<SizService>();
SchemaUpgrade.Apply(dbPath);
var app=builder.Build();
using(var scope=app.Services.CreateScope())await Seed.Initialize(scope.ServiceProvider.GetRequiredService<AppDbContext>());
if(!string.IsNullOrWhiteSpace(proxy))app.UseForwardedHeaders();
app.Use(async(ctx,next)=>{
    ctx.Response.Headers.XContentTypeOptions="nosniff";ctx.Response.Headers.XFrameOptions="DENY";
    ctx.Response.Headers["Referrer-Policy"]="same-origin";
    if(ctx.Request.Path.StartsWithSegments("/api"))ctx.Response.Headers.CacheControl="no-store";
    if(serverMode&&!ctx.Request.IsHttps&&ctx.Request.Path!="/api/health"){ctx.Response.StatusCode=400;return;}
    if(ctx.Request.IsHttps)ctx.Response.Headers.StrictTransportSecurity="max-age=31536000";
    try{await next();}
    catch(Exception ex){
        if(ctx.Response.HasStarted)throw;
        ctx.Response.StatusCode=ex switch{RuleException r=>r.Status,KeyNotFoundException=>404,DbUpdateException=>409,InvalidOperationException=>409,BadHttpRequestException b=>b.StatusCode,_=>500};
        var message=ex switch{RuleException r=>r.Message,KeyNotFoundException=>"Запись не найдена.",DbUpdateException=>"Операция нарушает связи или уникальность данных.",InvalidOperationException i when System.Text.RegularExpressions.Regex.IsMatch(i.Message,@"^[А-Яа-я]")=>i.Message,BadHttpRequestException=>"Некорректный запрос.",_=>"Не удалось выполнить операцию."};
        if(ctx.Response.StatusCode==500)app.Logger.LogError(ex,"Request failed: {Path}",ctx.Request.Path);
        await ctx.Response.WriteAsJsonAsync(new{message});
    }
});
app.UseDefaultFiles();app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
app.Use(async(ctx,next)=>{
    if(ctx.Request.Path.StartsWithSegments("/api")&&!HttpMethods.IsGet(ctx.Request.Method)&&!HttpMethods.IsHead(ctx.Request.Method)&&!HttpMethods.IsOptions(ctx.Request.Method)){
        try{await ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(ctx);}
        catch(Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{message="Проверка сессии не пройдена. Обновите страницу и повторите вход."});return;}
    }
    await next();
});
if(builder.Configuration.GetValue<bool?>("SwaggerEnabled")??!serverMode){
    app.Use(async(ctx,next)=>{if(ctx.Request.Path.StartsWithSegments("/swagger")&&ctx.User.Identity?.IsAuthenticated!=true){ctx.Response.StatusCode=401;return;}await next();});
    app.UseSwagger();app.UseSwaggerUI();
}
var writes=new SemaphoreSlim(1,1);
app.Use(async(ctx,next)=>{
    if(HttpMethods.IsGet(ctx.Request.Method)||HttpMethods.IsHead(ctx.Request.Method)){await next();return;}
    await writes.WaitAsync(ctx.RequestAborted);try{await next();}finally{writes.Release();}
});
Access.Map(app,operatorPassword,sessionLifetime);
app.MapGet("/api/time/resolve",(string local)=>{
    if(!DateTime.TryParseExact(local,"yyyy-MM-ddTHH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var value))return Results.BadRequest(new{message="Некорректное время."});
    return Results.Ok(new{utc=Clock.FromLocal(value),timeZone="Europe/Moscow"});
});
app.MapControllers();PersonnelApi.Map(app);SizApi.Map(app);OperationsApi.Map(app);ReportsApi.Map(app);
app.MapGet("/api/health",()=>Results.Ok(new{status="ok"})).AllowAnonymous();
app.MapGet("/api/settings",async(AppDbContext db)=>await db.Organizations.SingleAsync());
app.MapPut("/api/settings",async(Organization o,AppDbContext db)=>{Rules.Validate(o);var current=await db.Organizations.SingleAsync();o.Id=current.Id;db.Entry(current).CurrentValues.SetValues(o);await db.SaveChangesAsync();return Results.Ok(current);});
app.MapGet("/api/audit",async(AppDbContext db)=>await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.Id).Take(500).ToListAsync());
app.MapGet("/api/training/change-requests",async(AppDbContext db)=>await db.TrainingChangeRequests.AsNoTracking().OrderByDescending(x=>x.Id).ToListAsync());
app.MapFallback(async ctx=>{if(ctx.Request.Path.StartsWithSegments("/api")){ctx.Response.StatusCode=404;await ctx.Response.WriteAsJsonAsync(new{message="Маршрут API не найден."});}else{ctx.Response.StatusCode=404;}});
app.Run();
public partial class Program {}
