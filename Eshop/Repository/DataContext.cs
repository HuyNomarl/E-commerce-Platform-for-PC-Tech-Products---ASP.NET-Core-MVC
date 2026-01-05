using Eshop.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository
{
    public class DataContext : IdentityDbContext<AppUserModel>
    {
        public DataContext (DbContextOptions<DataContext> options) : base (options)
        {

        }
        public DbSet<Models.ProductModel> Products { get; set; }
        public DbSet<Models.CategoryModel> Categories { get; set; }
        public DbSet<Models.PublisherModel> Publishers { get; set; }
    }
}
