using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eshop.Models.ViewModels
{
    public class OrderDetailViewModel
    {
        public string OrderCode { get; set; }
        public string UserName { get; set; }

        public int Status { get; set; }

        public List<SelectListItem> StatusList { get; set; }

        public List<OrderDetails> OrderDetails { get; set; }
    }
}