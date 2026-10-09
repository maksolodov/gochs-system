namespace Gochs.Data;
public static class Seed
{
    public static async Task Initialize(AppDbContext db)
    {
        if(await db.Organizations.AnyAsync())return;
        await using var transaction=await db.Database.BeginTransactionAsync();
        var today=Clock.Today;var year=today.Year;
        db.Organizations.Add(new());db.SystemOperationalStates.Add(new(){Id=1,Mode=OperationalMode.Normal});
        var warehouse=new Warehouse{Name="Основной склад",Address="Учебная ул., 1"};db.Add(warehouse);
        var mask=new SizType{Name="Противогаз ГП-7",Category="GasMask",UnitPrice=5200,DeliveryDays=60,ShelfLifeYears=10,WasteClass="III-IV"};
        var medical=new SizType{Name="Аптечка КИМГЗ",Category="Medical",UnitPrice=4500,DeliveryDays=30,ShelfLifeYears=3,WasteClass="Г"};
        var suit=new SizType{Name="Защитный костюм Л-1",Category="Suit",UnitPrice=6800,DeliveryDays=45,ShelfLifeYears=5,WasteClass="ТКО"};
        db.AddRange(mask,medical,suit);
        var f=new Formation{Name="НФГО учебного музея",Type="Обеспечение мероприятий ГО",MchsCode="10.01",Location="Главный корпус"};db.Add(f);
        var unit=new NfgoUnit{Formation=f,Name="Звено связи",Capacity=3,Location="Пост охраны"};db.Add(unit);
        var shelter=new ProtectiveStructure{RegistrationNumber="УЧ-001",Name="Укрытие главного корпуса",ProtectionClass="Укрытие",Address="Учебная ул., 1, подвал",Capacity=30,NextInspectionDate=today.AddDays(30),ResponsiblePerson="Орлов А.В."};db.Add(shelter);
        db.Add(new ProtectiveStructure{RegistrationNumber="УЧ-002",Name="Резервное укрытие",ProtectionClass="Укрытие",Address="Учебная ул., 2",Capacity=15,Condition="Limited",NextInspectionDate=today.AddDays(7)});
        var vehicle=new Vehicle{Name="Учебный автобус",PlateNumber="УЧ-001",Capacity=20,DriverName="Миронов И.Е."};db.Add(vehicle);
        var point=new ReceptionPoint{Name="Загородный учебный центр",Address="Учебный поселок, 10",Capacity=50,ContactPerson="Смирнова А.Н.",ContactPhone="Учебный контакт"};db.Add(point);
        db.Add(new EvacuationRoute{Name="Основной маршрут",ReceptionPoint=point,StartPoint="Главный вход",DistanceKm=25,TravelTimeMinutes=45,Description="Учебная улица — загородный учебный центр"});
        var group=new TrainingGroup{Name="Группа № 1",Year=year,LeaderName="Орлов Алексей Викторович",LeaderPosition="Специалист ГО"};db.Add(group);
        await db.SaveChangesAsync();
        var names=new[]{"Орлов Алексей Викторович","Лебедева Марина Сергеевна","Соколов Денис Павлович","Миронова Анна Игоревна","Зайцев Павел Андреевич","Волкова Елена Дмитриевна"};
        var employees=names.Select((name,i)=>new Employee{PersonnelNumber=$"УЧ-{i+1:000}",FullName=name,Position=i==0?"Специалист ГО":"Сотрудник музея",Department=i<3?"Охрана":"Экспозиционный отдел",TrainingGroupId=group.Id,NfgoUnitId=i<3?unit.Id:null,NfgoRole=i==0?"Командир":"Боец",Reserved=i<3,UmcTrained=i==0,ProtectiveStructureId=shelter.Id,VehicleId=vehicle.Id,ReceptionPointId=point.Id,Shift=i%3+1}).ToList();
        db.AddRange(employees);db.EquipmentNorms.Add(new(){NfgoUnitId=unit.Id,SizTypeId=mask.Id,Quantity=3});
        db.Contractors.Add(new(){Name="Учебный переработчик",License="ДЕМО-001",ValidUntil=today.AddYears(2),WasteClasses="ТКО;III-IV;Г"});
        await db.SaveChangesAsync();
        foreach(var type in new[]{mask,medical,suit})
        {
            for(var y=0;y<5;y++)
            {
                var b=new SizBatch{SizTypeId=type.Id,WarehouseId=warehouse.Id,BatchNumber=$"ДЕМО-{type.Id}-{y}",Manufacturer="Учебный завод",Size=type==medical?"":"2",Shelf="А-"+(y+1),ManufactureDate=today.AddYears(-2),ExpiryDate=y==0?today.AddDays(-1):new DateOnly(year+y,6,30)};
                db.Add(b);for(var i=1;i<=3;i++)db.SizCards.Add(new(){SizBatch=b,InventoryNumber=$"УЧ-{type.Id}-{y}-{i:000}"});
            }
        }
        for(var i=0;i<3;i++)
        {
            var session=new TrainingSession{TrainingGroupId=group.Id,TopicNumber=i+1,TopicName=new[]{"Сигналы оповещения","Правила применения СИЗ","Первая помощь"}[i],Date=new DateOnly(year,1,1).AddDays(i),Hours=4,ClassType="Практика",LeaderName=group.LeaderName,Location="Учебный класс",IsSigned=true,SignedBy=group.LeaderName,SignedAt=DateTime.UtcNow};
            db.Add(session);foreach(var e in employees)db.AttendanceRecords.Add(new(){TrainingSession=session,EmployeeId=e.Id,Status=AttendanceStatus.Present,TestResult=i==2?TrainingTestResult.Passed:TrainingTestResult.None,TestScore=i==2?90:null});
        }
        db.TrainingSessions.Add(new(){TrainingGroupId=group.Id,TopicNumber=4,TopicName="Практическая тренировка эвакуации",Date=today.AddDays(3),Hours=3,ClassType="Практика",LeaderName=group.LeaderName,Location="Главный корпус"});
        db.Activities.AddRange(new Activity{Direction="Подготовка",Name="Проведение инструктажей",EmployeeId=employees[0].Id,Deadline=today.AddDays(10),Progress=80},new Activity{Direction="СИЗ",Name="Проверка резерва",EmployeeId=employees[1].Id,Deadline=today.AddDays(5),Progress=50},new Activity{Direction="Эвакуация",Name="Распределение персонала",EmployeeId=employees[2].Id,Deadline=today.AddDays(7),Progress=100});
        db.WartimeTransitionOrders.Add(new(){OrderNumber="УЧ-01-мп",OrganizationName="Учебный музей «Северный»",OrderDate=today,EffectiveFrom=DateTime.UtcNow.AddMinutes(-1),Basis="Учебное распоряжение для демонстрации",ResponsiblePerson=employees[0].FullName,CreatedBy=employees[0].FullName,WorkScheduleDescription="Три смены по 8 часов",AdditionalInstructions="Учебный сценарий, вымышленные данные."});
        await db.SaveChangesAsync();
        var service=new Gochs.Modules.Siz.SizService(db);
        var available=await db.SizCards.Where(x=>x.SizBatch!.SizTypeId==mask.Id&&x.SizBatch.ExpiryDate>today).OrderByDescending(x=>x.SizBatch!.ExpiryDate).FirstAsync();
        var statement=await service.IssueDraft(new("Охрана","Учебная выдача",[new(employees[0].Id,available.Id)]));await service.Confirm(statement.Id,new(employees[0].FullName));
        db.NfgoOrders.Add(new(){Number="УЧ-01-НФГО",Organization="Учебный музей «Северный»",Basis="Учебное формирование НФГО",Snapshot=await Gochs.Modules.Operations.OperationsApi.Snapshot(db)});
        Rules.Audit(db,"Созданы демонстрационные данные",new{Employees=employees.Count});await db.SaveChangesAsync();await transaction.CommitAsync();
    }
}
