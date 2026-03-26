using Eshop.Models;
using Eshop.Repository.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

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
        public DbSet<ProductQuantityModel> productQuantityModels { get; set; }
        public DbSet<ShippingModel> Shippings { get; set; }
        public DbSet<CouponModel> Coupons { get; set; }
        public DbSet<MessageModel> Messages { get; set; }
        public DbSet<ProductOptionGroupModel> ProductOptionGroups { get; set; }
        public DbSet<ProductOptionValueModel> ProductOptionValues { get; set; }
        public DbSet<SpecificationDefinitionModel> SpecificationDefinitions { get; set; }
        public DbSet<ProductSpecificationModel> ProductSpecifications { get; set; }
        public DbSet<PrebuiltPcComponentModel> PrebuiltPcComponents { get; set; }
        public DbSet<PcBuildModel> PcBuilds { get; set; }
        public DbSet<PcBuildItemModel> PcBuildItems { get; set; }
        public DbSet<ProductImageModel> ProductImages { get; set; }
        public DbSet<ProductTechnicalAssetModel> ProductTechnicalAssets { get; set; }
        public DbSet<WarehouseModel> Warehouses { get; set; }
        public DbSet<InventoryStockModel> InventoryStocks { get; set; }
        public DbSet<InventoryTransactionModel> InventoryTransactions { get; set; }
        public DbSet<InventoryTransactionDetailModel> InventoryTransactionDetails { get; set; }
        public DbSet<InventoryReservationModel> InventoryReservations { get; set; }
        public DbSet<InventoryReservationDetailModel> InventoryReservationDetails { get; set; }
        public DbSet<InventoryReceiptModel> InventoryReceipts { get; set; }
        public DbSet<InventoryReceiptDetailModel> InventoryReceiptDetails { get; set; }




        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<OrderModel>()
        .HasKey(x => x.OrderId);

            builder.Entity<OrderModel>()
        .HasIndex(x => x.OrderCode)
        .IsUnique();

            builder.Entity<OrderDetails>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderDetails>()
                .HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

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

            builder.Entity<MessageModel>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessageModel>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MessageModel>()
                .HasIndex(m => new { m.SenderId, m.ReceiverId });

            builder.Entity<MessageModel>()
                .HasIndex(m => m.CreatedAt);

            builder.Entity<CategoryModel>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductModel>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProductOptionGroupModel>()
                .HasOne(g => g.Product)
                .WithMany(p => p.OptionGroups)
                .HasForeignKey(g => g.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProductOptionValueModel>()
                .HasOne(v => v.ProductOptionGroup)
                .WithMany(g => g.OptionValues)
                .HasForeignKey(v => v.ProductOptionGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PrebuiltPcComponentModel>()
                .HasOne(x => x.Product)
                .WithMany(x => x.PrebuiltComponents)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PrebuiltPcComponentModel>()
                .HasOne(x => x.ComponentProduct)
                .WithMany()
                .HasForeignKey(x => x.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SpecificationDefinitionModel>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<ProductSpecificationModel>()
                .HasOne(x => x.Product)
                .WithMany(x => x.Specifications)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProductSpecificationModel>()
                .HasOne(x => x.SpecificationDefinition)
                .WithMany(x => x.ProductSpecifications)
                .HasForeignKey(x => x.SpecificationDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PcBuildItemModel>()
                .HasOne(x => x.PcBuild)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PcBuildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PcBuildItemModel>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            PcSpecificationSeed.Seed(builder);
            builder.Entity<ProductImageModel>()
              .HasOne(x => x.Product)
              .WithMany(x => x.ProductImages)
              .HasForeignKey(x => x.ProductId)
              .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProductTechnicalAssetModel>()
                .HasOne(x => x.Product)
                .WithOne(x => x.TechnicalAsset)
                .HasForeignKey<ProductTechnicalAssetModel>(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<WishlistModel>()
                .HasIndex(x => new { x.UserId, x.ProductId })
                .IsUnique();

            builder.Entity<WarehouseModel>()
                .HasIndex(x => x.Code)
                .IsUnique();

            builder.Entity<InventoryStockModel>()
                .HasIndex(x => new { x.ProductId, x.WarehouseId })
                .IsUnique();

            builder.Entity<InventoryStockModel>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InventoryStockModel>()
                .HasOne(x => x.Warehouse)
                .WithMany(x => x.InventoryStocks)
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransactionModel>()
                .HasOne(x => x.Warehouse)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryTransactionDetailModel>()
                .HasOne(x => x.InventoryTransaction)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InventoryTransactionDetailModel>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<InventoryTransactionModel>()
                .HasIndex(x => x.TransactionCode)
                .IsUnique();

            builder.Entity<InventoryTransactionModel>()
                .HasIndex(x => x.ReferenceCode);

            builder.Entity<InventoryReceiptModel>()
                .HasIndex(x => x.ReceiptCode)
                .IsUnique();

            builder.Entity<InventoryReceiptModel>()
                .HasIndex(x => x.Status);

            builder.Entity<InventoryReceiptModel>()
                .HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryReceiptModel>()
                .HasOne(x => x.Publisher)
                .WithMany()
                .HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryReceiptDetailModel>()
                .HasOne(x => x.InventoryReceipt)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InventoryReceiptDetailModel>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryReservationModel>()
                .HasIndex(x => x.ReservationCode)
                .IsUnique();

            builder.Entity<InventoryReservationModel>()
                .HasIndex(x => new { x.SessionId, x.Status });

            builder.Entity<InventoryReservationModel>()
                .HasIndex(x => x.ExpiresAt);

            builder.Entity<InventoryReservationDetailModel>()
                .HasOne(x => x.InventoryReservation)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InventoryReservationDetailModel>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryReservationDetailModel>()
                .HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);


        }

    }
}
