using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;

namespace PetWork.Controllers
{
    public class BlogController : Controller
    {
        private readonly PetWorkDbContext _context;

        public BlogController(PetWorkDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? category = null, string? sortBy = "newest")
        {
            // Veritabanından blog yazılarını çek
            var blogPosts = _context.BlogPosts
                .Include(b => b.User)
                .AsQueryable();

            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                blogPosts = blogPosts.Where(b => b.Category == category);
            }

            // Sıralama uygula
            blogPosts = sortBy switch
            {
                "oldest" => blogPosts.OrderBy(b => b.PublishDate),
                "mostviewed" => blogPosts.OrderByDescending(b => b.ViewCount),
                _ => blogPosts.OrderByDescending(b => b.PublishDate),
            };

            // Kategorileri veritabanından al
            var categories = _context.BlogPosts
                .Where(b => b.Category != null)
                .Select(b => b.Category)
                .Distinct()
                .ToList();

            // Veri yoksa varsayılan kategoriler
            if (categories.Count == 0)
            {
                categories = new List<string>
                {
                    "Kedi Bakımı",
                    "Köpek Bakımı",
                    "Köpek Eğitimi",
                    "Kedi Beslenmesi",
                    "Köpek Beslenmesi",
                    "Kuş Bakımı",
                    "Kemirgen Bakımı",
                    "Genel Bakım"
                };
            }

            var viewModel = new BlogViewModel
            {
                BlogPosts = blogPosts.ToList(),
                Categories = categories,
                SelectedCategory = category,
                SortBy = sortBy
            };

            return View(viewModel);
        }

        public IActionResult Details(int id)
        {
            // Veritabanından blog yazısını ID'ye göre bul
            var blogPost = _context.BlogPosts
                .Include(b => b.User)
                .FirstOrDefault(b => b.Id == id);

            if (blogPost == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            blogPost.ViewCount++;
            _context.SaveChanges();

            return View(blogPost);
        }
    }
} 