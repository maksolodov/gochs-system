using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Gochs.Data;

public static class SchemaUpgrade
{
    private static readonly string[] Scripts=["001_initial.sql","002_disposal_method.sql"];
    private static string Script(string name)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Gochs.Data.Migrations."+name)!;
        using var reader=new StreamReader(stream);return reader.ReadToEnd().Replace("\r\n","\n");
    }
    private static void Execute(SqliteConnection connection,string sql,SqliteTransaction? transaction=null)
    {
        using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;command.ExecuteNonQuery();
    }
    private static Dictionary<string,string> Schema(SqliteConnection c)
    {
        using var cmd=c.CreateCommand();cmd.CommandText="SELECT name, sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' AND name <> '__GochsMigrations' ORDER BY name";
        using var rows=cmd.ExecuteReader();var result=new Dictionary<string,string>();
        while(rows.Read())result.Add(rows.GetString(0),Regex.Replace(rows.GetString(1),@"\s+"," ").Trim());return result;
    }
    public static void Backup(string source,string destination)
    {
        if(Path.GetFullPath(source)==Path.GetFullPath(destination))throw new InvalidOperationException("Резервная копия должна иметь другой путь.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        using(var file=new FileStream(destination,FileMode.CreateNew)){}
        using var src=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString());src.Open();
        using var dst=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=destination}.ToString());dst.Open();src.BackupDatabase(dst);
        Check(dst);
    }
    public static void Restore(string source,string destination)
    {
        using var src=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly}.ToString());src.Open();Check(src);
        if(!Schema(src).ContainsKey("Employees"))throw new InvalidOperationException("Это не резервная копия ГОЧС.");
        if(File.Exists(destination))Backup(destination,destination+".before-restore-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        using var dst=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=destination}.ToString());dst.Open();src.BackupDatabase(dst);Check(dst);
    }
    private static void Check(SqliteConnection c)
    {
        using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA integrity_check;";
        if((string?)cmd.ExecuteScalar()!="ok")throw new InvalidOperationException("Проверка целостности SQLite не пройдена.");
    }
    public static void Apply(string path)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,ForeignKeys=true}.ToString());db.Open();
        using var cmd=db.CreateCommand();cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE name='__GochsMigrations'";
        bool tracked=(long)cmd.ExecuteScalar()!>0;
        var scripts=Scripts.Select(Script).ToArray();var hashes=scripts.Select(s=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))).ToArray();
        var applied=new Dictionary<int,string>();
        if(tracked){cmd.CommandText="SELECT Version, Hash FROM __GochsMigrations";using var reader=cmd.ExecuteReader();while(reader.Read())applied.Add(reader.GetInt32(0),reader.GetString(1));}
        // Совместимость с уже примененной исходной миграцией с окончаниями строк Windows.
        bool EquivalentInitial(int version,string hash)=>version==1&&hash=="3ECA85E85EE4AD3656DD550E103F062573125FBE13FACA2117D54AACDA6BECC1"&&hashes[0]=="CF7C7764550316ED1103BDBB0A1A6D4296981C62AB5B23F2AADDEA585F86C808";
        if(applied.Any(x=>x.Key<1||x.Key>Scripts.Length||(hashes[x.Key-1]!=x.Value&&!EquivalentInitial(x.Key,x.Value)))||!applied.Keys.Order().SequenceEqual(Enumerable.Range(1,applied.Count)))
            throw new InvalidOperationException("Версия схемы не соответствует приложению. Обновление остановлено без изменения данных.");
        if(applied.Count==Scripts.Length)return;
        var actual=Schema(db);bool legacy=!tracked&&actual.Count>0;
        if(actual.Count>0)Backup(path,Path.Combine(Path.GetDirectoryName(path)!,"backups","before-upgrade-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".db"));
        if(legacy)
        {
            using var reference=new SqliteConnection("Data Source=:memory:");reference.Open();Execute(reference,scripts[0]);var expected=Schema(reference);
            if(actual.Count!=expected.Count||actual.Any(x=>!expected.TryGetValue(x.Key,out var sql)||sql!=x.Value))
                throw new InvalidOperationException("Неизвестная исходная схема SQLite. Создана резервная копия; автоматическое обновление остановлено.");
        }
        using var tx=db.BeginTransaction();
        Execute(db,"CREATE TABLE IF NOT EXISTS __GochsMigrations (Version INTEGER PRIMARY KEY, Hash TEXT NOT NULL, AppliedUtc TEXT NOT NULL);",tx);
        for(int i=applied.Count;i<scripts.Length;i++)
        {
            if(!(legacy&&i==0))Execute(db,scripts[i],tx);
            using var insert=db.CreateCommand();insert.Transaction=tx;insert.CommandText="INSERT INTO __GochsMigrations VALUES ($v,$h,$at)";
            insert.Parameters.AddWithValue("$v",i+1);insert.Parameters.AddWithValue("$h",hashes[i]);insert.Parameters.AddWithValue("$at",DateTime.UtcNow.ToString("O"));insert.ExecuteNonQuery();
        }
        tx.Commit();Check(db);Execute(db,"PRAGMA journal_mode=WAL;");
    }
}
