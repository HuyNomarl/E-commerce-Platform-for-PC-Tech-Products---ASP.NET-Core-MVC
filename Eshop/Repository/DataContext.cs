using Eshop.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository
{
    public class DataContext : IdentityDbContext<AppUserModel>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }
        public DbSet<Models.ProductModel> Products { get; set; }
        public DbSet<Models.CategoryModel> Categories { get; set; }
        public DbSet<Models.PublisherModel> Publishers { get; set; }
        public DbSet<OrderModel> Orders { get; set; }
        public DbSet<OrderDetails> OrderDetails { get; set; }
        public DbSet<RatingModel> RatingModels { get; set; }
        public DbSet<SliderModel> Sliders { get; set; }
        public DbSet<ContactModel> Contact { get; set; }
        public DbSet<CompareModel> Compares { get; set; }
        public DbSet<WishlistModel> Wishlists { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppUserModel>(b =>
            {
                b.Property(x => x.RoleId).HasMaxLength(450);

                b.HasOne<IdentityRole>()
                 .WithMany()
                 .HasForeignKey(x => x.RoleId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<OrderModel>()
                   .HasKey(o => o.OrderId);

            builder.Entity<OrderDetails>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);
        }

    }
}
