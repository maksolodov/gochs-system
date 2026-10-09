using System.Text.Json;
using System.Text;
using System.Xml.Linq;
using System.Net;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S=DocumentFormat.OpenXml.Spreadsheet;
using W=DocumentFormat.OpenXml.Wordprocessing;
namespace Gochs.Modules.Reports;
public record PlanRow(int Year,string Name,int Quantity,int Expiring,int Deficit,decimal UnitPrice,decimal Total,DateOnly DeliveryDate,string Size);
public record ReadinessRow(int Id,string Name,string Code,int Required,int Personnel,int Vacancies,double EquipmentPercent,double TrainingPercent,double Readiness);
public record EquipmentRow(string Name,int Required,int Available,int Fit,double Percent);
public record NfgoReport(string Organization,string Category,DateOnly Date,string Status,List<ReadinessRow> Formations,List<EquipmentRow> Equipment,int Employees,int Trained,int UmcCommanders);
public static class ReportsApi
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/training/protocol/export/{format}",async(string format,int groupId,int year,Gochs.Modules.Training.Services.ITrainingService service)=>{
            var protocol=await service.GetProtocolAsync(groupId,year)??throw new RuleException("Учебная группа не найдена.",404);
            Rules.Require(protocol.Ready,"Порог 12 часов для протокола не достигнут.");
            return Export(format,"training-protocol","Учебный протокол: "+protocol.GroupName+" / "+year,
                [new("Результаты",["ФИО","Часов","Результат"],protocol.Employees.Select(x=>new object?[]{x.FullName,x.AttendedHours,x.TestResult==TrainingTestResult.Passed?"Зачтено":x.TestResult==TrainingTestResult.Failed?"Не зачтено":"Нет результата"}).ToList()),
                 new("Подписи комиссии",["Председатель","Член комиссии","Член комиссии"],[["________________","________________","________________"]])]);
        });
        app.MapGet("/api/reports/nfgo",BuildReport);
        app.MapGet("/api/reports/procurement",async(int? startYear,AppDbContext db)=>await Plan(db,startYear??Clock.Today.Year));
        app.MapGet("/api/dashboard",async(AppDbContext db)=>{
            var report=await BuildReport(db);var cards=await db.SizCards.AsNoTracking().Include(x=>x.SizBatch).ToListAsync();var activities=await db.Activities.AsNoTracking().ToListAsync();
            return new{report.Organization,report.Employees,formations=report.Formations.Count,inStock=cards.Count(x=>x.Status=="InStock"),issued=cards.Count(x=>x.Status=="Issued"),writtenOff=cards.Count(x=>x.Status=="WrittenOff"),disposed=cards.Count(x=>x.Status=="Disposed"),expiring=cards.Count(x=>x.Status is "InStock" or "Issued"&&x.SizBatch!.ExpiryDate<=Clock.Today.AddMonths(6)),activityProgress=activities.Count==0?0:Math.Round(activities.Average(x=>x.Progress),1),activities=activities.Count,training=report.Trained,mode=(await db.SystemOperationalStates.SingleAsync()).Mode,evacuation=await Gochs.Modules.Operations.OperationsApi.Summary(db)};
        });
        app.MapGet("/api/reports/nfgo/export/{format}",async(string format,bool? final,AppDbContext db)=>{
            var report=await BuildReport(db);Rules.Require(final!=true||report.Formations.All(x=>x.Vacancies==0),"Финальная выгрузка заблокирована: есть вакансии или уволенные сотрудники.");
            if(format=="json")return Results.File(JsonSerializer.SerializeToUtf8Bytes(report,new JsonSerializerOptions{WriteIndented=true}),"application/json","nfgo.json");
            if(format=="xml")return Results.File(Encoding.UTF8.GetBytes(new XElement("NfgoReport",new XElement("Organization",report.Organization),new XElement("Category",report.Category),new XElement("Date",report.Date),new XElement("Status",report.Status),new XElement("Formations",report.Formations.Select(f=>new XElement("Formation",new XAttribute("id",f.Id),new XElement("Name",f.Name),new XElement("Code",f.Code),new XElement("Personnel",f.Personnel),new XElement("Vacancies",f.Vacancies),new XElement("Readiness",f.Readiness)))),new XElement("Equipment",report.Equipment.Select(e=>new XElement("Item",new XElement("Name",e.Name),new XElement("Required",e.Required),new XElement("Available",e.Available),new XElement("Fit",e.Fit)))),new XElement("Training",new XElement("Employees",report.Employees),new XElement("Trained",report.Trained),new XElement("UmcCommanders",report.UmcCommanders))).ToString()),"application/xml","nfgo.xml");
            var tables=ReportTables(report);return Export(format,"nfgo","Сведения о наличии и готовности НФГО — "+report.Organization,tables);
        });
        app.MapGet("/api/reports/procurement/export/{format}",async(string format,int? startYear,AppDbContext db)=>{
            var year=startYear??Clock.Today.Year;var rows=await Plan(db,year);var table=new ExportTable("План закупок",["Год","СИЗ","Размер","Замена","Дефицит","К закупке","Цена, руб.","Сумма, руб.","Поставка до"],rows.Select(x=>new object?[]{x.Year,x.Name,x.Size,x.Expiring,x.Deficit,x.Quantity,x.UnitPrice,x.Total,x.DeliveryDate}).ToList());
            return Export(format,"procurement",$"План закупок СИЗ на {year}–{year+4} гг.",[table]);
        });
        app.MapGet("/api/documents/{kind}/{id:int}/{format}",async(string kind,int id,string format,AppDbContext db)=>{
            var org=await db.Organizations.SingleAsync();var tables=new List<ExportTable>();string title;
            if(kind=="statement"){
                var s=await db.IssueStatements.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Документ не найден.",404);
                title=$"Ведомость {s.Number} от {s.Date:dd.MM.yyyy}. {s.Department}. {s.Basis}. {s.Status}";
                tables.Add(new("Выдача",["ФИО","Табельный №","СИЗ","Партия","Размер","Кол-во","Подпись"],s.Lines.Select(x=>new object?[]{x.EmployeeName,x.PersonnelNumber,x.ItemName,x.BatchNumber,x.Size,1,"________"}).ToList()));
            }else if(kind=="writeoff"||kind=="disposal"){
                Disposal? disposal=kind=="disposal"?await Rules.Find<Disposal>(db,id):null;
                var actId=disposal?.WriteOffActId??id;
                var act=await db.WriteOffActs.Include(x=>x.Lines).ThenInclude(x=>x.SizCard).ThenInclude(x=>x!.SizBatch).ThenInclude(x=>x!.SizType).SingleOrDefaultAsync(x=>x.Id==actId)??throw new RuleException("Акт не найден.",404);
                if(disposal!=null){
                    var contractor=await Rules.Find<Contractor>(db,disposal.ContractorId);
                    title=$"Документ утилизации: {disposal.TransferDocument}, дата {disposal.Date:dd.MM.yyyy}";
                    tables.Add(new("Передача имущества",["Реквизит","Значение"],[["Акт списания",act.Number],["Дата списания",act.Date],["Контрагент",contractor.Name],["Лицензия",contractor.License],["Договор",disposal.ContractNumber],["Документ передачи",disposal.TransferDocument],["Фактическая дата утилизации",disposal.Date],["Способ утилизации",string.IsNullOrWhiteSpace(disposal.Method)?"Не указан":disposal.Method]]));
                }else{
                    title=$"Акт списания {act.Number} от {act.Date:dd.MM.yyyy}";
                    tables.Add(new("Основание списания",["Реквизит","Значение"],[["Комиссия",act.Commission],["Причина",act.Reason],["Статус акта",act.Status],["Подписант",act.SignedBy??"Не подписан"]]));
                }
                tables.Add(new("Имущество",["Инвентарный номер","СИЗ","Партия","Срок годности","Класс отходов"],act.Lines.Select(x=>new object?[]{x.SizCard!.InventoryNumber,x.SizCard.SizBatch!.SizType!.Name,x.SizCard.SizBatch.BatchNumber,x.SizCard.SizBatch.ExpiryDate,x.SizCard.SizBatch.SizType.WasteClass}).ToList()));
            }else if(kind=="nfgo"){
                var o=await Rules.Find<NfgoOrder>(db,id);title=$"Приказ № {o.Number} от {o.Date:dd.MM.yyyy} о создании НФГО. Основание: {o.Basis}. {o.Status}";
                using var doc=JsonDocument.Parse(o.Snapshot);var members=doc.RootElement.GetProperty("members");
                var units=doc.RootElement.GetProperty("units").EnumerateArray().ToDictionary(x=>x.GetProperty("Id").GetInt32(),x=>x.GetProperty("Name").GetString());
                tables.Add(new("Личный состав",["Табельный №","ФИО","Должность","Звено","Роль"],members.EnumerateArray().Select(x=>new object?[]{x.GetProperty("PersonnelNumber").GetString(),x.GetProperty("FullName").GetString(),x.GetProperty("Position").GetString(),units.GetValueOrDefault(x.GetProperty("NfgoUnitId").GetInt32()),x.GetProperty("NfgoRole").GetString()}).ToList()));
            }else if(kind=="card"){
                var c=await db.SizCards.Include(x=>x.SizBatch).ThenInclude(x=>x!.SizType).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Карточка не найдена.",404);
                title="Карточка учета СИЗ "+c.InventoryNumber;var b=c.SizBatch!;
                tables.Add(new("Паспорт",["Поле","Значение"],[ ["Наименование",b.SizType!.Name],["Производитель",b.Manufacturer],["Партия",b.BatchNumber],["Размер",b.Size],["Изготовлено",b.ManufactureDate],["Годен до",b.ExpiryDate],["Стеллаж",b.Shelf],["Статус",c.Status],["Сотрудник",c.EmployeeId.HasValue?(await Rules.Find<Employee>(db,c.EmployeeId.Value)).FullName:"На складе"] ]));
            }else throw new RuleException("Неизвестный документ.",404);
            return Export(format,kind+"-"+id,org.Name+". "+title,tables);
        });
    }
    public static async Task<NfgoReport> BuildReport(AppDbContext db)
    {
        var org=await db.Organizations.AsNoTracking().SingleAsync();var people=await db.Employees.AsNoTracking().ToListAsync();var units=await db.NfgoUnits.AsNoTracking().Include(x=>x.Formation).ToListAsync();var norms=await db.EquipmentNorms.AsNoTracking().ToListAsync();var cards=await db.SizCards.AsNoTracking().Include(x=>x.SizBatch).ToListAsync();var types=await db.SizTypes.AsNoTracking().ToListAsync();
        var attendance=await db.AttendanceRecords.AsNoTracking().Include(x=>x.TrainingSession).Where(x=>x.TrainingSession!.IsSigned&&x.TrainingSession.Date.Year==Clock.Today.Year).ToListAsync();
        var trained=attendance.GroupBy(x=>x.EmployeeId).Where(g=>g.Where(x=>x.Status==AttendanceStatus.Present).Sum(x=>x.TrainingSession!.Hours)>=15&&g.OrderByDescending(x=>x.UpdatedAt).FirstOrDefault(x=>x.TestResult!=TrainingTestResult.None)?.TestResult==TrainingTestResult.Passed).Select(g=>g.Key).ToHashSet();
        bool Fit(SizCard c)=>c.Status is "InStock" or "Issued"&&c.Condition=="Fit"&&c.SizBatch!.ExpiryDate>Clock.Today;
        var rows=new List<ReadinessRow>();
        foreach(var u in units)
        {
            var members=people.Where(x=>x.NfgoUnitId==u.Id&&x.PersonnelStatus!=PersonnelStatus.Dismissed).ToList();var ns=norms.Where(x=>x.NfgoUnitId==u.Id).ToList();
            var equipment=ns.Count==0?0:ns.Min(n=>Math.Min(100,100.0*cards.Count(c=>c.NfgoUnitId==u.Id&&c.SizBatch!.SizTypeId==n.SizTypeId&&Fit(c))/n.Quantity));
            var training=members.Count==0?0:100.0*members.Count(x=>trained.Contains(x.Id))/members.Count;
            rows.Add(new(u.Id,u.Name,u.Formation!.MchsCode,u.Capacity,members.Count,Math.Max(0,u.Capacity-members.Count),Math.Round(equipment,1),Math.Round(training,1),Math.Round(Math.Min(100.0*members.Count/u.Capacity,Math.Min(equipment,training)),1)));
        }
        var active=people.Count(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed);
        var equipmentRows=types.Select(t=>{var required=Math.Max(t.PerEmployee*active,norms.Where(n=>n.SizTypeId==t.Id).Sum(n=>n.Quantity));var available=cards.Count(c=>c.SizBatch!.SizTypeId==t.Id&&c.Status is "InStock" or "Issued" or "Inspection");var fit=cards.Count(c=>c.SizBatch!.SizTypeId==t.Id&&Fit(c));return new EquipmentRow(t.Name,required,available,fit,required==0?100:Math.Round(Math.Min(100,100.0*fit/required),1));}).ToList();
        return new(org.Name,org.Category,Clock.Today,rows.Any(x=>x.Vacancies>0)?"Требует уточнения":"Готов к учебной выгрузке",rows,equipmentRows,active,people.Count(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed&&trained.Contains(x.Id)),people.Count(x=>x.NfgoRole=="Командир"&&x.UmcTrained&&x.PersonnelStatus!=PersonnelStatus.Dismissed));
    }
    public static async Task<List<PlanRow>> Plan(AppDbContext db,int year)
    {
        Rules.Require(year>=Clock.Today.Year&&year<=2100,"Начальный год должен быть не раньше текущего и не позже 2100.");
        var cards=await db.SizCards.AsNoTracking().Include(x=>x.SizBatch).ToListAsync();var types=await db.SizTypes.AsNoTracking().ToListAsync();var people=await db.Employees.AsNoTracking().Where(x=>x.PersonnelStatus!=PersonnelStatus.Dismissed).ToListAsync();var norms=await db.EquipmentNorms.AsNoTracking().ToListAsync();var result=new List<PlanRow>();
        foreach(var t in types)
        {
            var sizes=t.Category is "GasMask" or "Suit"?new[]{"1","2","3","4"}:new[]{""};
            foreach(var size in sizes)
            {
                var staff=t.Category switch{"GasMask"=>people.Count(x=>x.GasMaskSize.ToString()==size),"Suit"=>people.Count(x=>x.SuitSize.ToString()==size),_=>people.Count};
                var need=staff*t.PerEmployee;
                if(sizes.Length==1)need=Math.Max(need,norms.Where(x=>x.SizTypeId==t.Id).Sum(x=>x.Quantity));
                var stock=cards.Where(x=>x.SizBatch!.SizTypeId==t.Id&&(sizes.Length==1||x.SizBatch.Size==size)&&x.Status is "InStock" or "Issued"&&x.Condition=="Fit").Select(x=>x.SizBatch!.ExpiryDate).ToList();
                for(var y=year;y<year+5;y++)
                {
                    var end=new DateOnly(y,12,31);var start=new DateOnly(y,1,1);var expired=stock.Count(x=>x<=end);var lost=stock.Where(x=>x<=end).ToList();stock.RemoveAll(x=>x<=end);
                    var quantity=Math.Max(0,need-stock.Count);var replacement=Math.Min(expired,quantity);var deficit=quantity-replacement;
                    var due=lost.Count>0?lost.Min().AddDays(-t.DeliveryDays):start.AddDays(t.DeliveryDays);if(due<start)due=start;
                    result.Add(new(y,t.Name,quantity,replacement,deficit,t.UnitPrice,quantity*t.UnitPrice,due,size));
                    // Будущие закупки пополняют расчетный остаток, чтобы дефицит не повторялся каждый год.
                    for(var n=0;n<quantity;n++)stock.Add(due.AddYears(t.ShelfLifeYears));
                }
            }
        }
        return result.OrderBy(x=>x.Year).ThenBy(x=>x.Name).ThenBy(x=>x.Size).ToList();
    }
    private static List<ExportTable> ReportTables(NfgoReport r)=>[
        new("Формирования",["Звено","Код МЧС","Штат","Состав","Вакансии","Оснащение %","Обучение %","Готовность %"],r.Formations.Select(x=>new object?[]{x.Name,x.Code,x.Required,x.Personnel,x.Vacancies,x.EquipmentPercent,x.TrainingPercent,x.Readiness}).ToList()),
        new("Оснащение",["Имущество","Потребность","Наличие","Годных","Обеспеченность %"],r.Equipment.Select(x=>new object?[]{x.Name,x.Required,x.Available,x.Fit,x.Percent}).ToList()),
        new("Подготовка",["Показатель","Значение"],[["Сотрудников",r.Employees],["Обучены (15 часов и зачет)",r.Trained],["Командиры УМЦ",r.UmcCommanders],["Статус",r.Status]])];
    public static IResult Export(string format,string filename,string title,List<ExportTable> tables)
    {
        if(format=="html")return Results.Content(Html(title,tables),"text/html; charset=utf-8");
        using var ms=new MemoryStream();
        if(format=="xlsx")
        {
            using(var doc=SpreadsheetDocument.Create(ms,SpreadsheetDocumentType.Workbook))
            {
                var wb=doc.AddWorkbookPart();wb.Workbook=new S.Workbook();var sheets=wb.Workbook.AppendChild(new S.Sheets());uint id=1;
                foreach(var table in tables)
                {
                    var part=wb.AddNewPart<WorksheetPart>();var data=new S.SheetData();part.Worksheet=new S.Worksheet(new S.Columns(Enumerable.Range(1,table.Headers.Length).Select(i=>new S.Column{Min=(uint)i,Max=(uint)i,Width=i==1?32:24,CustomWidth=true})),data);
                    data.Append(Row([title]));data.Append(Row(table.Headers.Cast<object?>().ToArray()));foreach(var row in table.Rows)data.Append(Row(row));
                    sheets.Append(new S.Sheet{Id=wb.GetIdOfPart(part),SheetId=id++,Name=table.Name[..Math.Min(31,table.Name.Length)]});
                }
            }
            return Results.File(ms.ToArray(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",filename+".xlsx");
        }
        if(format=="docx")
        {
            using(var doc=WordprocessingDocument.Create(ms,WordprocessingDocumentType.Document))
            {
                var part=doc.AddMainDocumentPart();var body=new W.Body();part.Document=new W.Document(body);body.Append(Paragraph(title));
                foreach(var table in tables){body.Append(Paragraph(table.Name));var t=new W.Table();foreach(var row in new[]{table.Headers.Cast<object?>().ToArray()}.Concat(table.Rows))t.Append(new W.TableRow(row.Select(x=>new W.TableCell(Paragraph(Convert.ToString(x)??"")))));body.Append(t);}
                body.Append(Paragraph("Учебный документ. Регистрация подписания не является УКЭП."));
            }
            return Results.File(ms.ToArray(),"application/vnd.openxmlformats-officedocument.wordprocessingml.document",filename+".docx");
        }
        throw new RuleException("Форматы: html, xlsx, docx.",400);
    }
    private static W.Paragraph Paragraph(string text)=>new(new W.Run(new W.Text(text)));
    private static S.Row Row(object?[] cells)=>new(cells.Select(x=>x is int or decimal or double?new S.Cell{DataType=S.CellValues.Number,CellValue=new S.CellValue(Convert.ToString(x,System.Globalization.CultureInfo.InvariantCulture)??"0")}:new S.Cell{DataType=S.CellValues.InlineString,InlineString=new S.InlineString(new S.Text(Convert.ToString(x)??""))}));
    private static string Html(string title,List<ExportTable> tables)
    {
        string E(object? x)=>WebUtility.HtmlEncode(Convert.ToString(x))??"";
        var s=new StringBuilder("<!doctype html><html lang='ru'><meta charset='utf-8'><title>"+E(title)+"</title><style>body{font:14px Arial;margin:30px;color:#172033}h1{font-size:21px}table{border-collapse:collapse;width:100%;margin:20px 0}th,td{border:1px solid #bbc5d3;padding:8px;text-align:left}th{background:#edf2f7}@media print{button{display:none}thead{display:table-header-group}tr{break-inside:avoid}}</style><button onclick='window.print()'>Печать / сохранить PDF</button><h1>"+E(title)+"</h1>");
        foreach(var t in tables){s.Append("<h2>"+E(t.Name)+"</h2><table><thead><tr>"+string.Join("",t.Headers.Select(x=>"<th>"+E(x)+"</th>"))+"</tr></thead><tbody>");foreach(var row in t.Rows)s.Append("<tr>"+string.Join("",row.Select(x=>"<td>"+E(x)+"</td>"))+"</tr>");s.Append("</tbody></table>");}
        return s+"<p>Учебный документ. Регистрация подписания не является УКЭП.</p></html>";
    }
}
public record ExportTable(string Name,string[] Headers,List<object?[]> Rows);
