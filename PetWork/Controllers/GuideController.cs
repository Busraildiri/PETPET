using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;

namespace PetWork.Controllers
{
    public class GuideController : Controller
    {
        private readonly PetWorkDbContext _context;

        public GuideController(PetWorkDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? animalType = null, string? category = null)
        {
            // Veritabanından rehberleri çek
            var guides = _context.Guides
                .Include(g => g.User)
                .AsQueryable();

            // Hayvan türü filtresi uygula
            if (!string.IsNullOrEmpty(animalType))
            {
                guides = guides.Where(g => g.AnimalType == animalType);
            }

            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                guides = guides.Where(g => g.Category == category);
            }

            // Varsayılan olarak görüntülenme sayısına göre sırala
            guides = guides.OrderByDescending(g => g.ViewCount);

            // Hayvan türlerini veritabanından al
            var animalTypes = _context.Guides
                .Where(g => g.AnimalType != null)
                .Select(g => g.AnimalType)
                .Distinct()
                .ToList();

            // Kategorileri veritabanından al
            var categories = _context.Guides
                .Where(g => g.Category != null)
                .Select(g => g.Category)
                .Distinct()
                .ToList();

            // Veri yoksa varsayılan değerler
            if (animalTypes.Count == 0)
            {
                animalTypes = new List<string>
                {
                    "Kedi",
                    "Köpek",
                    "Kuş",
                    "Akvaryum",
                    "Kemirgen",
                    "Tavşan",
                    "Sürüngen",
                    "Diğer"
                };
            }

            if (categories.Count == 0)
            {
                categories = new List<string>
                {
                    "Temel Bakım",
                    "Eğitim",
                    "Beslenme",
                    "Kurulum",
                    "Hastalık Bakımı",
                    "Davranış",
                    "Yaşlanma Bakımı"
                };
            }

            var viewModel = new GuideViewModel
            {
                Guides = guides.ToList(),
                AnimalTypes = animalTypes,
                Categories = categories,
                SelectedAnimalType = animalType,
                SelectedCategory = category
            };

            return View(viewModel);
        }

        public IActionResult Details(int id)
        {
            // Veritabanından rehberi ID'ye göre bul
            var guide = _context.Guides
                .Include(g => g.User)
                .FirstOrDefault(g => g.Id == id);

            if (guide == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            guide.ViewCount++;
            _context.SaveChanges();
            ViewBag.ExternalSource = _context.ExternalContentSources.AsNoTracking().FirstOrDefault(x =>
                x.ContentType == ExternalContentTypes.Guide && x.LocalContentId == guide.Id &&
                x.ReviewStatus != ExternalContentReviewStatuses.Rejected && x.ReviewStatus != ExternalContentReviewStatuses.Archived);

            return View(guide);
        }
    }
} 
