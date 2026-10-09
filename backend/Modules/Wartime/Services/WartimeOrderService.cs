using System.Net;
using Microsoft.EntityFrameworkCore;
using Gochs.Data;
using Gochs.Modules.Wartime.DTOs;
using Gochs.Modules.Wartime.Models;

namespace Gochs.Modules.Wartime.Services;

public class WartimeOrderService : IWartimeOrderService
{
    private readonly AppDbContext _db;

    public WartimeOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<WartimeTransitionOrder>> GetOrdersAsync()
    {
        return await _db.WartimeTransitionOrders
            .AsNoTracking()
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<WartimeTransitionOrder?> GetOrderAsync(int id)
    {
        return await _db.WartimeTransitionOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<WartimeTransitionOrder> CreateOrderAsync(WartimeOrderUpsertDto dto)
    {
        var numberExists = await _db.WartimeTransitionOrders
            .AnyAsync(x => x.OrderNumber == dto.OrderNumber.Trim());

        if (numberExists)
            throw new InvalidOperationException("Приказ с таким номером уже существует.");

        ValidateDates(dto.OrderDate, dto.EffectiveFrom);

        var order = new WartimeTransitionOrder
        {
            OrderNumber = dto.OrderNumber.Trim(),
            OrganizationName = dto.OrganizationName.Trim(),
            OrderDate = dto.OrderDate,
            EffectiveFrom = dto.EffectiveFrom.UtcDateTime,
            Basis = dto.Basis.Trim(),
            ResponsiblePerson = dto.ResponsiblePerson.Trim(),
            ResponsiblePosition = dto.ResponsiblePosition?.Trim(),
            NotificationProcedure = dto.NotificationProcedure?.Trim(),
            WorkScheduleDescription = dto.WorkScheduleDescription?.Trim(),
            AdditionalInstructions = dto.AdditionalInstructions?.Trim(),
            CreatedBy = dto.CreatedBy.Trim(),
            Status = WartimeOrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.WartimeTransitionOrders.Add(order);
        await _db.SaveChangesAsync();

        await AddActionAsync(order.Id, "OrderCreated", $"Создан проект приказа №{order.OrderNumber}.");
        await _db.SaveChangesAsync();

        return order;
    }

    public async Task<WartimeTransitionOrder?> UpdateOrderAsync(int id, WartimeOrderUpsertDto dto)
    {
        var order = await _db.WartimeTransitionOrders.FindAsync(id);
        if (order is null)
            return null;

        if (order.Status != WartimeOrderStatus.Draft)
            throw new InvalidOperationException("Изменять можно только проект приказа.");

        var numberExists = await _db.WartimeTransitionOrders
            .AnyAsync(x => x.Id != id && x.OrderNumber == dto.OrderNumber.Trim());

        if (numberExists)
            throw new InvalidOperationException("Приказ с таким номером уже существует.");

        ValidateDates(dto.OrderDate, dto.EffectiveFrom);

        order.OrderNumber = dto.OrderNumber.Trim();
        order.OrganizationName = dto.OrganizationName.Trim();
        order.OrderDate = dto.OrderDate;
        order.EffectiveFrom = dto.EffectiveFrom.UtcDateTime;
        order.Basis = dto.Basis.Trim();
        order.ResponsiblePerson = dto.ResponsiblePerson.Trim();
        order.ResponsiblePosition = dto.ResponsiblePosition?.Trim();
        order.NotificationProcedure = dto.NotificationProcedure?.Trim();
        order.WorkScheduleDescription = dto.WorkScheduleDescription?.Trim();
        order.AdditionalInstructions = dto.AdditionalInstructions?.Trim();
        order.CreatedBy = dto.CreatedBy.Trim();

        await AddActionAsync(order.Id, "OrderUpdated", $"Изменен проект приказа №{order.OrderNumber}.");
        await _db.SaveChangesAsync();

        return order;
    }

    public async Task<WartimeTransitionOrder?> SignOrderAsync(int id, SignWartimeOrderDto dto)
    {
        var order = await _db.WartimeTransitionOrders.FindAsync(id);
        if (order is null)
            return null;

        if (order.Status != WartimeOrderStatus.Draft)
            throw new InvalidOperationException("Подписать можно только проект приказа.");

        order.Status = WartimeOrderStatus.Signed;
        order.SignedBy = dto.SignedBy.Trim();
        order.SignedAt = DateTime.UtcNow;

        await AddActionAsync(order.Id, "OrderSigned", $"Приказ подписан: {order.SignedBy}.");
        await _db.SaveChangesAsync();

        return order;
    }

    public async Task<WartimeTransitionOrder?> ActivateOrderAsync(int id)
    {
        var order = await _db.WartimeTransitionOrders.FindAsync(id);
        if (order is null)
            return null;

        if (order.Status != WartimeOrderStatus.Signed)
            throw new InvalidOperationException("Активировать можно только подписанный приказ.");

        if (order.EffectiveFrom > DateTime.UtcNow)
            throw new InvalidOperationException("Дата вступления приказа в силу еще не наступила.");

        var state = await GetOrCreateStateEntityAsync();

        if (state.Mode == OperationalMode.Wartime)
            throw new InvalidOperationException("Система уже переведена в режим военного времени.");

        await Gochs.Modules.Wartime.ModeActions.Activate(_db, order);
        order.Status = WartimeOrderStatus.Activated;
        order.ActivatedAt = DateTime.UtcNow;

        state.Mode = OperationalMode.Wartime;
        state.ActiveOrderId = order.Id;
        state.ActivatedAt = order.ActivatedAt;
        state.SpecialWorkScheduleEnabled = true;
        state.LeaveRestrictionsEnabled = true;
        state.EmergencyNotificationsEnabled = true;
        state.ProtectedFormsEnabled = true;
        state.UpdatedAt = DateTime.UtcNow;

        await AddActionAsync(order.Id, "OrderActivated", $"Активирован приказ №{order.OrderNumber}.");
        await AddActionAsync(order.Id, "WartimeModeEnabled", "Система переведена в режим военного времени.");
        await AddActionAsync(order.Id, "SpecialWorkScheduleEnabled", "Включен специальный режим рабочего графика.");
        await AddActionAsync(order.Id, "LeaveRestrictionsEnabled", "Включены ограничения на операции, связанные с отпусками.");
        await AddActionAsync(order.Id, "EmergencyNotificationsEnabled", "Включен режим экстренных оповещений.");
        await AddActionAsync(order.Id, "ProtectedFormsEnabled", "Включено использование защищенных форм.");

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<WartimeOrderStateDto> GetStateAsync()
    {
        var state = await GetOrCreateStateEntityAsync();
        await _db.SaveChangesAsync();

        return new WartimeOrderStateDto
        {
            Mode = state.Mode,
            ActiveOrderId = state.ActiveOrderId,
            ActivatedAt = state.ActivatedAt,
            SpecialWorkScheduleEnabled = state.SpecialWorkScheduleEnabled,
            LeaveRestrictionsEnabled = state.LeaveRestrictionsEnabled,
            EmergencyNotificationsEnabled = state.EmergencyNotificationsEnabled,
            ProtectedFormsEnabled = state.ProtectedFormsEnabled
        };
    }

    public async Task<List<WartimeActionLog>> GetAuditAsync(int orderId)
    {
        return await _db.WartimeActionLogs
            .AsNoTracking()
            .Where(x => x.WartimeTransitionOrderId == orderId)
            .OrderBy(x => x.ExecutedAt)
            .ToListAsync();
    }

    public async Task<string?> GetDocumentHtmlAsync(int id)
    {
        var order = await _db.WartimeTransitionOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order is null)
            return null;

        string e(string? value) => WebUtility.HtmlEncode(value) ?? "";
        var signed = string.IsNullOrWhiteSpace(order.SignedBy)
            ? "____________________"
            : e(order.SignedBy);

        var notification = string.IsNullOrWhiteSpace(order.NotificationProcedure)
            ? "Организовать оповещение работников в установленном порядке."
            : e(order.NotificationProcedure);

        var schedule = string.IsNullOrWhiteSpace(order.WorkScheduleDescription)
            ? "Ввести специальный режим рабочего графика в соответствии с утвержденным планом."
            : e(order.WorkScheduleDescription);

        var additional = string.IsNullOrWhiteSpace(order.AdditionalInstructions)
            ? "Дополнительные указания отсутствуют."
            : e(order.AdditionalInstructions);

        return $$"""
<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<title>Приказ №{{e(order.OrderNumber)}}</title>
<style>
body { font-family: Arial, sans-serif; max-width: 850px; margin: 40px auto; color: #111; line-height: 1.45; }
h1, h2 { text-align: center; }
.meta { display: flex; justify-content: space-between; margin: 30px 0; }
p { text-align: justify; }
.signature { margin-top: 50px; }
</style>
</head>
<body>
<h2>{{e(order.OrganizationName)}}</h2>
<h1>ПРИКАЗ</h1><p>Учебный документ. Регистрация подписания не является УКЭП.</p>
<div class="meta">
<span>{{order.OrderDate:dd.MM.yyyy}}</span>
<span>№ {{e(order.OrderNumber)}}</span>
</div>
<h2>О переводе на режим военного времени</h2>
<p>Основание: {{e(order.Basis)}}</p>
<p>ПРИКАЗЫВАЮ:</p>
<p>1. С {{Clock.Local(order.EffectiveFrom):dd.MM.yyyy HH:mm}} (Москва) перевести {{e(order.OrganizationName)}} на режим военного времени.</p>
<p>2. {{notification}}</p>
<p>3. {{schedule}}</p>
<p>4. Включить режим экстренных оповещений, ограничения на операции, связанные с отпусками, и использование защищенных форм информационной системы.</p>
<p>5. Ответственным за выполнение настоящего приказа назначить {{e(order.ResponsiblePerson)}}{{(string.IsNullOrWhiteSpace(order.ResponsiblePosition) ? "" : $", {e(order.ResponsiblePosition)}")}}.</p>
<p>6. {{additional}}</p>
<p>7. Контроль исполнения настоящего приказа возложить на ответственное должностное лицо.</p>
<div class="signature">
<p>Руководитель: {{signed}}</p>
</div>
</body>
</html>
""";
    }

    private async Task<SystemOperationalState> GetOrCreateStateEntityAsync()
    {
        var state = await _db.SystemOperationalStates.FirstOrDefaultAsync(x => x.Id == 1);
        if (state is not null)
            return state;

        state = new SystemOperationalState
        {
            Id = 1,
            Mode = OperationalMode.Normal,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SystemOperationalStates.Add(state);
        return state;
    }

    private async Task AddActionAsync(int orderId, string action, string details)
    {
        await _db.WartimeActionLogs.AddAsync(new WartimeActionLog
        {
            WartimeTransitionOrderId = orderId,
            Action = action,
            Details = details,
            ExecutedAt = DateTime.UtcNow
        });
    }

    private static void ValidateDates(DateOnly orderDate, DateTimeOffset effectiveFrom)
    {
        if (Clock.Local(effectiveFrom.UtcDateTime).Date < orderDate.ToDateTime(TimeOnly.MinValue).Date)
            throw new InvalidOperationException("Дата вступления приказа в силу не может быть раньше даты приказа.");
    }
}
