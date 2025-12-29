using Eshop.Models;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository
{
    public class SeedData
    {
        public static void SeedingData(DataContext _context)
        {
            //_context.Database.Migrate();
            if (!_context.Products.Any())
            {
                CategoryModel Romance = new CategoryModel { Name = "Romance", Description = "Romance Books", Slug = "romance", Status = 1 };
                CategoryModel ScienceFiction = new CategoryModel { Name = "Science Fiction", Description = "Science Fiction Books", Slug = "science-fiction", Status = 1 };
                CategoryModel Horror = new CategoryModel { Name = "Horror", Description = "Horror Books", Slug = "horror", Status = 1 };
                PublisherModel Penguin = new PublisherModel { Name = "Penguin Random House", Description = "Leading publisher of books", Slug = "penguin-random-house", status = 1 };
                PublisherModel HarperCollins = new PublisherModel { Name = "HarperCollins", Description = "Global publisher of books", Slug = "harpercollins", status = 1 };
                _context.Products.AddRange(
                    new ProductModel { Name = "Love in the Time of Cholera", Slug = "love-in-the-time-of-cholera", Description = "A novel by Gabriel Garcia Marquez", Price = 9.99M, Category = Romance, Publisher = Penguin, Image = "love_in_the_time_of_cholera.jpg" },
                    new ProductModel { Name = "Dune", Slug = "dune", Description = "A science fiction novel by Frank Herbert", Price = 14.99M, Category = ScienceFiction, Publisher = HarperCollins, Image = "dune.jpg" }
                );
                _context.SaveChanges();
            }
        }
    }
}
