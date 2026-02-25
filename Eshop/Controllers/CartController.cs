using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _dataContext;
        public CartController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public IActionResult Index()
        {
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemViewModel cartItemViewModel = new()
            {
                CartItems = cartItems,
                GrandTotal = cartItems.Sum(x => x.Quantity * x.Price)
            };
            return View(cartItemViewModel);
        }
        public IActionResult Checkout()
        {
            return View("~/Views/Checkout/Index.cshtml");
        }


        public async Task<IActionResult> Add(int id)
        {
            ProductModel product = await _dataContext.Products.FindAsync(id);
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();
            if (cartItem == null)
            {
                cart.Add(new CartItemModel(product));
            }
            else
            {
                cartItem.Quantity += 1;
            }
            HttpContext.Session.SetJson("Cart", cart);

            TempData["Success"] = "Product added to cart successfully!";

            return Redirect(Request.Headers["Referer"].ToString());
        }

        public async Task<IActionResult> Decrease(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();

            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
            }
            else
            {
                cart.RemoveAll(x => x.ProductId == id);
            }

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cart updated (-) successfully!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Increase(int id)
        {
            ProductModel product = await _dataContext.Products.Where(x => x.Id == id).FirstOrDefaultAsync();
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();

            if (cartItem.Quantity >= 1 && product.Quantity > cartItem.Quantity)
            {
                ++cartItem.Quantity;
            }
            else
            {
                cartItem.Quantity = product.Quantity;
                //cart.RemoveAll(x => x.ProductId == id);
                TempData["Error"] = "Cannot increase quantity. Not enough stock available.";
            }

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cart updated (+) successfully!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");
            cart.RemoveAll(x => x.ProductId == id);
            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Clear()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

    }
}
