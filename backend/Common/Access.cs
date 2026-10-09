using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Caching.Memory;
namespace Gochs.Common;
public record LoginRequest(string Password,string Name);
public sealed class OperatorSessions
{
    private readonly MemoryCache cache=new(new MemoryCacheOptions{SizeLimit=10000});
    public bool Blocked(string address)=>cache.TryGetValue<int>("fail:"+address,out var n)&&n>=5;
    public void Failed(string address){var key="fail:"+address;cache.TryGetValue<int>(key,out var n);cache.Set(key,n+1,new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=TimeSpan.FromMinutes(15),Size=1});}
    public string Create(string address,TimeSpan lifetime){cache.Remove("fail:"+address);var id=Guid.NewGuid().ToString("N");cache.Set("session:"+id,true,new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=lifetime,Size=1});return id;}
    public bool Valid(string? id)=>id!=null&&cache.TryGetValue("session:"+id,out _);
    public void Remove(string? id){if(id!=null)cache.Remove("session:"+id);}
}
public static class Access
{
    public static string PreparePassword(string directory,IConfiguration config)
    {
        var file=config["OperatorPasswordFile"];
        var configured=string.IsNullOrWhiteSpace(file)?config["OperatorPassword"]:File.ReadAllText(file).Trim();
        if(config.GetValue<bool>("ServerMode")&&string.IsNullOrWhiteSpace(configured))throw new InvalidOperationException("Для серверного режима задайте секрет OperatorPasswordFile или OperatorPassword.");
        if(!string.IsNullOrWhiteSpace(configured)){
            if(configured.Length<16)throw new InvalidOperationException("Пароль должен содержать не менее 16 символов.");return configured;
        }
        var path=Path.Combine(directory,"operator-password.txt");
        if(File.Exists(path))return File.ReadAllText(path).Trim();
        var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        File.WriteAllText(path,password,Encoding.UTF8);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        return password;
    }
    public static void Map(WebApplication app,string password,TimeSpan lifetime)
    {
        app.MapGet("/api/auth/token",(HttpContext ctx,IAntiforgery antiforgery)=>new{token=antiforgery.GetAndStoreTokens(ctx).RequestToken}).AllowAnonymous();
        app.MapGet("/api/auth/session",(HttpContext ctx)=>new{authenticated=ctx.User.Identity?.IsAuthenticated==true,name=ctx.User.Identity?.Name}).AllowAnonymous();
        app.MapPost("/api/auth/login",async(LoginRequest input,HttpContext ctx,OperatorSessions sessions)=>{
            var address=ctx.Connection.RemoteIpAddress?.ToString()??"local";
            if(sessions.Blocked(address)){ctx.Response.Headers.RetryAfter="900";return Results.Json(new{message="Слишком много попыток входа. Повторите через 15 минут."},statusCode:429);}
            var actual=SHA256.HashData(Encoding.UTF8.GetBytes(input.Password??""));var expected=SHA256.HashData(Encoding.UTF8.GetBytes(password));
            if(!CryptographicOperations.FixedTimeEquals(actual,expected)){sessions.Failed(address);return Results.Json(new{message="Не удалось войти. Проверьте данные."},statusCode:401);}
            var name=Rules.Text(input.Name,"Имя оператора");Rules.Require(name.Length<=100,"Имя оператора слишком длинное.");
            sessions.Remove(ctx.User.FindFirstValue("sid"));
            var identity=new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"operator"),new Claim(ClaimTypes.Name,name),new Claim("sid",sessions.Create(address,lifetime))],CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));return Results.Ok(new{authenticated=true,name});
        }).AllowAnonymous();
        app.MapPost("/api/auth/logout",async(HttpContext ctx,OperatorSessions sessions)=>{sessions.Remove(ctx.User.FindFirstValue("sid"));await ctx.SignOutAsync();return Results.NoContent();});
    }
}
