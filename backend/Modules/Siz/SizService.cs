namespace Gochs.Modules.Siz;
public record BatchReceipt(SizBatch Batch,int Quantity,string InventoryPrefix);
public record IssueRequest(string Department,string Basis,List<IssueChoice> Lines);
public record IssueChoice(int EmployeeId,int SizCardId);
public record AutoIssueRequest(string Department,int SizTypeId,string Basis);
public record WriteOffRequest(string Commission,string Reason,List<int> CardIds);
public record Signature(string SignedBy);
public record CardCondition(string Condition);
public record BulkBatchUpdate(List<int> BatchIds,int WarehouseId,string Shelf,DateOnly? LastInspectionDate,string InspectionNumber);

public class SizService(AppDbContext db)
{
    public async Task<SizBatch> Receive(BatchReceipt dto)
    {
        Rules.Validate(dto.Batch);Rules.Require(dto.Quantity is >0 and <=10000,"Количество должно быть от 1 до 10000.");
        var prefix=Rules.Text(dto.InventoryPrefix,"Префикс инвентарного номера");
        Rules.Require(prefix.Length<=60,"Префикс должен быть не длиннее 60 символов.");
        await Rules.Find<SizType>(db,dto.Batch.SizTypeId);await Rules.Find<Warehouse>(db,dto.Batch.WarehouseId);
        Rules.Require(dto.Batch.ExpiryDate>dto.Batch.ManufactureDate,"Срок годности должен быть позже даты изготовления.");
        Rules.Require(dto.Batch.ReceivedDate>=dto.Batch.ManufactureDate,"Приход не может быть раньше изготовления.");
        Rules.Require(!await db.SizBatches.AnyAsync(b=>b.SizTypeId==dto.Batch.SizTypeId&&b.BatchNumber==dto.Batch.BatchNumber&&b.Size==dto.Batch.Size),"Такая партия и размер уже приняты. Укажите номер нового поступления.");
        var existing=await db.SizCards.Where(c=>c.InventoryNumber.StartsWith(prefix+"-")).Select(c=>c.InventoryNumber).ToListAsync();
        long last=existing.Select(n=>long.TryParse(n[(prefix.Length+1)..],out var value)?value:0).DefaultIfEmpty(0).Max();
        Rules.Require(last<=long.MaxValue-dto.Quantity,"Диапазон инвентарных номеров исчерпан. Выберите другой префикс.");
        dto.Batch.Id=0;db.SizBatches.Add(dto.Batch);
        for(var i=1;i<=dto.Quantity;i++)db.SizCards.Add(new(){SizBatch=dto.Batch,InventoryNumber=$"{prefix}-{last+i:0000}"});
        Rules.Audit(db,"Приход СИЗ",new{dto.Quantity,dto.Batch.BatchNumber});await db.SaveChangesAsync();return dto.Batch;
    }
    public async Task<IssueStatement> IssueDraft(IssueRequest dto,int? id=null)
    {
        Rules.Text(dto.Department,"Подразделение");Rules.Text(dto.Basis,"Основание");
        Rules.Require(dto.Lines.Count>0,"Выберите сотрудников и СИЗ.");
        Rules.Require(dto.Lines.Select(x=>x.SizCardId).Distinct().Count()==dto.Lines.Count,"Одна карточка указана несколько раз.");
        var lines=new List<IssueLine>();
        foreach(var choice in dto.Lines)
        {
            var e=await Rules.Find<Employee>(db,choice.EmployeeId);
            var c=await db.SizCards.Include(x=>x.SizBatch).ThenInclude(x=>x!.SizType).SingleOrDefaultAsync(x=>x.Id==choice.SizCardId)??throw new RuleException("Карточка не найдена.",404);
            CheckIssue(e,c,dto.Department);
            lines.Add(new(){EmployeeId=e.Id,SizCardId=c.Id,EmployeeName=e.FullName,PersonnelNumber=e.PersonnelNumber,ItemName=c.SizBatch!.SizType!.Name,BatchNumber=c.SizBatch.BatchNumber,Size=c.SizBatch.Size});
        }
        var s=id.HasValue?await db.IssueStatements.Include(x=>x.Lines).SingleAsync(x=>x.Id==id):new IssueStatement{Number=Rules.Number("ВЕД")};
        Rules.Require(s.Status=="Draft","Подтвержденную ведомость нельзя редактировать.");
        if(id.HasValue)db.IssueLines.RemoveRange(s.Lines);else db.Add(s);
        s.Department=dto.Department;s.Basis=dto.Basis;s.Lines=lines;
        Rules.Audit(db,"Проект ведомости",new{s.Number});await db.SaveChangesAsync();return s;
    }
    private static void CheckIssue(Employee e,SizCard c,string department)
    {
        Rules.Require(e.PersonnelStatus!=PersonnelStatus.Dismissed,"Сотрудник уволен.");
        Rules.Require(e.Department==department,"Сотрудник не входит в подразделение ведомости.");
        Rules.Require(c.Status=="InStock"&&c.Condition=="Fit","Изделие отсутствует на складе или непригодно.");
        Rules.Require(c.SizBatch!.ExpiryDate>Clock.Today,"Нельзя выдать просроченное изделие.");
        var size=c.SizBatch.SizType!.Category switch{"GasMask"=>e.GasMaskSize.ToString(),"Suit"=>e.SuitSize.ToString(),_=>c.SizBatch.Size};
        Rules.Require(c.SizBatch.Size==size,"Размер изделия не соответствует сотруднику.");
    }
    public async Task<IssueStatement> AutoIssue(AutoIssueRequest dto)
    {
        var people=await db.Employees.Where(x=>x.Department==dto.Department&&x.PersonnelStatus!=PersonnelStatus.Dismissed).OrderByDescending(x=>x.NfgoUnitId!=null).ThenBy(x=>x.Id).ToListAsync();
        var cards=await db.SizCards.Include(x=>x.SizBatch).ThenInclude(x=>x!.SizType).Where(x=>x.SizBatch!.SizTypeId==dto.SizTypeId&&x.Status=="InStock"&&x.Condition=="Fit"&&x.SizBatch.ExpiryDate>Clock.Today).OrderByDescending(x=>x.SizBatch!.ExpiryDate).ToListAsync();
        var lines=new List<IssueChoice>();
        foreach(var e in people)
        {
            var c=cards.FirstOrDefault(c=>c.SizBatch!.Size==(c.SizBatch.SizType!.Category switch{"GasMask"=>e.GasMaskSize.ToString(),"Suit"=>e.SuitSize.ToString(),_=>c.SizBatch.Size}));
            Rules.Require(c!=null,$"Не хватает годного СИЗ нужного размера для {e.FullName}. Ведомость не создана.");
            lines.Add(new(e.Id,c!.Id));cards.Remove(c);
        }
        return await IssueDraft(new(dto.Department,dto.Basis,lines));
    }
    public async Task<IssueStatement> Confirm(int id,Signature dto)
    {
        var signer=Rules.Text(dto.SignedBy,"Подписант");
        var s=await db.IssueStatements.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Ведомость не найдена.",404);
        Rules.Require(s.Status=="Draft","Ведомость уже подтверждена.");
        foreach(var l in s.Lines)
        {
            var e=await Rules.Find<Employee>(db,l.EmployeeId);
            var c=await db.SizCards.Include(x=>x.SizBatch).ThenInclude(x=>x!.SizType).SingleAsync(x=>x.Id==l.SizCardId);
            CheckIssue(e,c,s.Department);
            l.EmployeeName=e.FullName;l.PersonnelNumber=e.PersonnelNumber;
            c.Status="Issued";c.EmployeeId=e.Id;c.NfgoUnitId=e.NfgoUnitId;c.IssueDate=Clock.Today;
        }
        s.Status="Confirmed";s.Date=Clock.Today;s.SignedBy=signer;
        Rules.Audit(db,"Подтверждена выдача",new{s.Number,s.SignedBy});await db.SaveChangesAsync();return s;
    }
    public async Task<WriteOffAct> DraftWriteOff(WriteOffRequest dto)
    {
        Rules.Text(dto.Commission,"Комиссия");Rules.Text(dto.Reason,"Причина");
        var ids=dto.CardIds.Distinct().ToList();Rules.Require(ids.Count>0,"Выберите изделия.");
        var cards=await db.SizCards.Include(x=>x.SizBatch).Where(x=>ids.Contains(x.Id)).ToListAsync();
        Rules.Require(cards.Count==ids.Count,"Часть карточек не найдена.");
        foreach(var c in cards)CheckWriteOff(c);
        var act=new WriteOffAct{Number=Rules.Number("АКТ"),Commission=dto.Commission,Reason=dto.Reason,Lines=ids.Select(id=>new WriteOffLine{SizCardId=id}).ToList()};
        db.Add(act);Rules.Audit(db,"Проект списания",new{act.Number,ids});await db.SaveChangesAsync();return act;
    }
    private static void CheckWriteOff(SizCard c)
    {
        Rules.Require(c.Status is "InStock" or "Issued" or "Inspection","Изделие уже списано или утилизировано.");
        Rules.Require(c.SizBatch!.ExpiryDate<=Clock.Today||c.Condition=="Unfit","Списание доступно только для просроченных или негодных СИЗ.");
    }
    public async Task<WriteOffAct> SignWriteOff(int id,Signature dto)
    {
        var act=await db.WriteOffActs.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.Id==id)??throw new RuleException("Акт не найден.",404);
        Rules.Require(act.Status=="Draft","Акт уже подписан.");act.SignedBy=Rules.Text(dto.SignedBy,"Подписант");
        foreach(var l in act.Lines)
        {
            var c=await db.SizCards.Include(x=>x.SizBatch).SingleAsync(x=>x.Id==l.SizCardId);CheckWriteOff(c);c.Status="WrittenOff";c.Condition="Unfit";
        }
        act.Status="Signed";act.Date=Clock.Today;Rules.Audit(db,"Подписано списание",new{act.Number,act.SignedBy});await db.SaveChangesAsync();return act;
    }
    public async Task<Disposal> Dispose(Disposal dto)
    {
        Rules.Validate(dto);Rules.Require(dto.Date<=Clock.Today,"Дата утилизации не может быть в будущем.");
        var act=await db.WriteOffActs.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.Id==dto.WriteOffActId)??throw new RuleException("Акт не найден.",404);
        Rules.Require(act.Status=="Signed","Сначала подпишите акт списания.");Rules.Require(dto.Date>=act.Date,"Утилизация не может быть раньше списания.");
        var contractor=await Rules.Find<Contractor>(db,dto.ContractorId);
        Rules.Require(contractor.ValidUntil>=dto.Date,"Лицензия контрагента истекла.");
        foreach(var l in act.Lines)
        {
            var c=await db.SizCards.Include(x=>x.SizBatch).ThenInclude(x=>x!.SizType).SingleAsync(x=>x.Id==l.SizCardId);
            Rules.Require(c.Status=="WrittenOff","Изделие уже утилизировано или не списано.");
            Rules.Require(contractor.WasteClasses.Split(';').Select(x=>x.Trim()).Contains(c.SizBatch!.SizType!.WasteClass),"Лицензия не покрывает класс отходов изделия.");c.Status="Disposed";
        }
        dto.Id=0;dto.Status="Completed";db.Add(dto);Rules.Audit(db,"Утилизация",new{dto.WriteOffActId,dto.ContractNumber});await db.SaveChangesAsync();return dto;
    }
}
