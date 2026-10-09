using Gochs.Modules.Wartime.DTOs;
using Gochs.Modules.Wartime.Models;

namespace Gochs.Modules.Wartime.Services;

public interface IWartimeOrderService
{
    Task<List<WartimeTransitionOrder>> GetOrdersAsync();
    Task<WartimeTransitionOrder?> GetOrderAsync(int id);
    Task<WartimeTransitionOrder> CreateOrderAsync(WartimeOrderUpsertDto dto);
    Task<WartimeTransitionOrder?> UpdateOrderAsync(int id, WartimeOrderUpsertDto dto);
    Task<WartimeTransitionOrder?> SignOrderAsync(int id, SignWartimeOrderDto dto);
    Task<WartimeTransitionOrder?> ActivateOrderAsync(int id);
    Task<WartimeOrderStateDto> GetStateAsync();
    Task<List<WartimeActionLog>> GetAuditAsync(int orderId);
    Task<string?> GetDocumentHtmlAsync(int id);
}
