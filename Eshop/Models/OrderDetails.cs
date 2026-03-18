using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class OrderDetails
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string OrderCode { get; set; }
        public int  ProductId { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        [ForeignKey(nameof(ProductId))]
        public ProductModel Product { get; set; }
        //Build PC
        public string? BuildGroupKey { get; set; }
        public int? PcBuildId { get; set; }
        public string? BuildName { get; set; }
        public string? ComponentType { get; set; }


    }
}
