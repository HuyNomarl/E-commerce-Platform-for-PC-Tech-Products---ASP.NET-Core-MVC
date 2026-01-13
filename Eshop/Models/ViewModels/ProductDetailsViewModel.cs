using Eshop.Models;
using System.ComponentModel.DataAnnotations;

public class ProductDetailsViewModel
{
    public ProductModel ProductDetail { get; set; }
    //public IEnumerable<RatingModel> RatingDetail { get; set; }
    //public RatingModel NewRating { get; set; } = new();
    [Required(ErrorMessage = "Vui lòng nhập bình luận của bạn.")]
    public string Comment { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên của bạn.")]
    public string Name { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập email của bạn.")]
    public string Email { get; set; }



}
