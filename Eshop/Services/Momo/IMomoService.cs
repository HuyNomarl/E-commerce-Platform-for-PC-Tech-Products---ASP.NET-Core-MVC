using Eshop.Models;
using Eshop.Models.Momo;
using Microsoft.AspNetCore.Http;

namespace Eshop.Services.Momo
{
    public interface IMomoService
    {
        Task<MomoCreatePaymentResponseModel> CreatePaymentAsync(OrderInfoModel model);
        MomoExecuteResponseModel PaymentExecuteAsync(IQueryCollection collection);
    }
}