using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;

namespace PetWork.Controllers
{
    public class DiseasesController : Controller
    {
        private readonly PetWorkDbContext _context;

        public DiseasesController(PetWorkDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string? animalType = null, string? category = null, string? severityLevel = null, string? sortOption = "En Yeni", DateTime? startDate = null, DateTime? endDate = null)
        {
            // Veritabanından hastalık bilgilerini çek
            var diseases = _context.Diseases.AsQueryable();

            // Hayvan türü filtresi uygula
            if (!string.IsNullOrEmpty(animalType))
            {
                diseases = diseases.Where(d => d.AnimalType == animalType);
            }

            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                diseases = diseases.Where(d => d.Category == category);
            }
            
            // Ciddiyet seviyesi filtresi uygula
            if (!string.IsNullOrEmpty(severityLevel))
            {
                diseases = diseases.Where(d => d.SeverityLevel == severityLevel);
            }
            
            // Tarih filtresi uygula
            if (startDate.HasValue)
            {
                diseases = diseases.Where(d => d.PublishDate >= startDate.Value);
            }
            
            if (endDate.HasValue)
            {
                diseases = diseases.Where(d => d.PublishDate <= endDate.Value);
            }
            
            // Sıralama seçeneğini uygula
            switch (sortOption)
            {
                case "En Yeni":
                    diseases = diseases.OrderByDescending(d => d.PublishDate);
                    break;
                case "En Eski":
                    diseases = diseases.OrderBy(d => d.PublishDate);
                    break;
                case "En Popüler":
                    diseases = diseases.OrderByDescending(d => d.ViewCount);
                    break;
                case "En Az Popüler":
                    diseases = diseases.OrderBy(d => d.ViewCount);
                    break;
                default:
                    diseases = diseases.OrderByDescending(d => d.PublishDate);
                    break;
            }

            // Hayvan türlerini veritabanından al
            var animalTypes = _context.Diseases
                .Where(d => d.AnimalType != null)
                .Select(d => d.AnimalType)
                .Distinct()
                .ToList();

            // Kategorileri veritabanından al
            var categories = _context.Diseases
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .ToList();

            // Ciddiyet seviyelerini veritabanından al
            var severityLevels = _context.Diseases
                .Where(d => d.SeverityLevel != null)
                .Select(d => d.SeverityLevel)
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
                    "Kemirgen",
                    "Akvaryum",
                    "Sürüngen",
                    "Diğer"
                };
            }

            if (categories.Count == 0)
            {
                categories = new List<string>
                {
                    "Viral Hastalıklar",
                    "Bakteriyel Hastalıklar",
                    "Paraziter Hastalıklar",
                    "Genetik Hastalıklar",
                    "Kronik Hastalıklar",
                    "Acil Durumlar"
                };
            }
            
            if (severityLevels.Count == 0)
            {
                severityLevels = new List<string>
                {
                    "Düşük",
                    "Orta",
                    "Yüksek"
                };
            }

            var viewModel = new DiseaseViewModel
            {
                Diseases = diseases.ToList(),
                AnimalTypes = animalTypes,
                Categories = categories,
                SeverityLevels = severityLevels,
                SelectedAnimalType = animalType,
                SelectedCategory = category,
                SelectedSeverityLevel = severityLevel,
                SelectedSortOption = sortOption,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(viewModel);
        }

        public IActionResult Details(int id)
        {
            // Veritabanından hastalık bilgisini ID'ye göre bul
            var disease = _context.Diseases.FirstOrDefault(d => d.Id == id);

            if (disease == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            disease.ViewCount++;
            _context.SaveChanges();

            return View(disease);
        }
    }
} 