using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository
{
    public class DataContext : DbContext
    {
        public DataContext (DbContextOptions<DataContext> options) : base (options)
        {

        }
        public DbSet<Models.ProductModel> Products { get; set; }
        public DbSet<Models.CategoryModel> Categories { get; set; }
        public DbSet<Models.PublisherModel> Publishers { get; set; }
    }
}
