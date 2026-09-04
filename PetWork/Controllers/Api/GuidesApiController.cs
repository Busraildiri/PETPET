using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers.Api
{
    [Route("api/guides")]
    [ApiController]
    public class GuidesApiController : ControllerBase
    {
        private readonly PetWorkDbContext _context;

        public GuidesApiController(PetWorkDbContext context)
        {
            _context = context;
        }

        // GET: api/Guides
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Guide>>> GetGuides(string category = null, string sortBy = "newest")
        {
            var guides = _context.Guides
                .Include(g => g.User)
                .AsQueryable();

            // Kategori filtresi
            if (!string.IsNullOrEmpty(category))
            {
                guides = guides.Where(g => g.Category == category);
            }

            // Sıralama
            guides = sortBy switch
            {
                "oldest" => guides.OrderBy(g => g.PublishDate),
                "alphabetical" => guides.OrderBy(g => g.Title),
                "mostviewed" => guides.OrderByDescending(g => g.ViewCount),
                _ => guides.OrderByDescending(g => g.PublishDate)
            };

            return await guides.ToListAsync();
        }

        // GET: api/Guides/featured
        [HttpGet("featured")]
        public async Task<ActionResult<IEnumerable<object>>> GetFeaturedGuides()
        {
            var featuredGuides = await _context.Guides
                .Include(g => g.User)
                .OrderByDescending(g => g.ViewCount)
                .Take(6)
                .Select(g => new
                {
                    id = g.Id,
                    title = g.Title,
                    description = g.Description,
                    category = g.Category ?? "Genel",
                    animalType = g.AnimalType,
                    level = g.Level,
                    viewCount = g.ViewCount
                })
                .ToListAsync();
                
            // If no guides exist, return sample data
            if (featuredGuides == null || !featuredGuides.Any())
            {
                var sampleData = new List<object>
                {
                    new {
                        id = 1,
                        title = "Evde Kedi Bakımı Rehberi",
                        description = "Yeni kedi sahipleri için temel bakım, besleme ve eğitim önerileri.",
                        imageUrl = "/img/guides/guide1.jpg",
                        category = "Bakım",
                        animalType = "Kedi",
                        level = "Başlangıç",
                        viewCount = 230
                    },
                    new {
                        id = 2,
                        title = "Köpek Eğitimi: Temel Komutlar",
                        description = "Köpeğinize temel komutları öğretme ve olumlu pekiştirme teknikleri.",
                        imageUrl = "/img/guides/guide2.jpg",
                        category = "Eğitim",
                        animalType = "Köpek",
                        level = "Orta",
                        viewCount = 185
                    },
                    new {
                        id = 3,
                        title = "Kuş Kafesi Düzenleme",
                        description = "Muhabbet kuşları ve papağanlar için ideal kafes düzeni ve gerekli aksesuarlar.",
                        imageUrl = "/img/guides/guide3.jpg",
                        category = "Bakım",
                        animalType = "Kuş",
                        level = "Başlangıç",
                        viewCount = 128
                    },
                    new {
                        id = 4,
                        title = "Kedi Tırmalama Davranışını Yönetme",
                        description = "Kedilerin tırmalama davranışını anlama ve mobilyalarınızı koruma yöntemleri.",
                        imageUrl = "/img/guides/guide4.jpg",
                        category = "Davranış",
                        animalType = "Kedi",
                        level = "Orta",
                        viewCount = 165
                    },
                    new {
                        id = 5,
                        title = "Köpek Yürüyüşleri: Tasma Çekmeyi Önleme",
                        description = "Köpeğinizin tasma çekme davranışını düzeltme ve keyifli yürüyüşler için ipuçları.",
                        imageUrl = "/img/guides/guide5.jpg",
                        category = "Eğitim",
                        animalType = "Köpek",
                        level = "İleri",
                        viewCount = 143
                    },
                    new {
                        id = 6,
                        title = "Akvaryum Kurulumu ve Bakımı",
                        description = "Yeni başlayanlar için adım adım akvaryum kurulumu ve balık bakımı rehberi.",
                        imageUrl = "/img/guides/guide6.jpg",
                        category = "Bakım",
                        animalType = "Balık",
                        level = "Başlangıç",
                        viewCount = 112
                    }
                };
                
                return Ok(sampleData);
            }

            return Ok(featuredGuides);
        }

        // GET: api/Guides/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Guide>> GetGuide(int id)
        {
            var guide = await _context.Guides
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (guide == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            guide.ViewCount++;
            await _context.SaveChangesAsync();

            return guide;
        }
    }
} 
