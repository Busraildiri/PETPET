using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Validation;

namespace PetWork.Controllers.Api
{
    [Route("api/diseases")]
    [ApiController]
    public class DiseasesApiController : ControllerBase
    {
        private readonly PetWorkDbContext _context;

        public DiseasesApiController(PetWorkDbContext context)
        {
            _context = context;
        }

        // GET: api/Diseases
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Disease>>> GetDiseases(string petType = null, string sortBy = "newest")
        {
            var diseases = _context.Diseases.AsQueryable();

            // Evcil hayvan türü filtresi
            if (!string.IsNullOrEmpty(petType))
            {
                diseases = diseases.Where(d => d.PetType == petType);
            }

            // Sıralama
            diseases = sortBy switch
            {
                "oldest" => diseases.OrderBy(d => d.PublishDate),
                "alphabetical" => diseases.OrderBy(d => d.Name),
                "mostviewed" => diseases.OrderByDescending(d => d.ViewCount),
                _ => diseases.OrderByDescending(d => d.PublishDate)
            };

            return await diseases.ToListAsync();
        }

        // GET: api/Diseases/featured
        [HttpGet("featured")]
        public async Task<ActionResult<IEnumerable<object>>> GetFeaturedDiseases()
        {
            var featuredDiseases = await _context.Diseases
                .OrderByDescending(d => d.ViewCount)
                .Take(6)
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Name,
                    description = d.Description.Length > 200 ? d.Description.Substring(0, 200) + "..." : d.Description,
                    imageUrl = d.FeaturedImage,
                    category = d.Category ?? "Genel",
                    animalType = d.AnimalType ?? d.PetType,
                    viewCount = d.ViewCount
                })
                .ToListAsync();
                
            // If no diseases exist, return sample data
            if (featuredDiseases == null || !featuredDiseases.Any())
            {
                var sampleData = new List<object>
                {
                    new {
                        id = 1,
                        title = "Kedi Üst Solunum Yolu Enfeksiyonu",
                        description = "Kedilerde görülen, hapşırma ve burun akıntısı gibi belirtiler gösteren yaygın bir üst solunum yolu rahatsızlığı.",
                        imageUrl = "/img/diseases/disease1.jpg",
                        category = "Solunum",
                        animalType = "Kedi",
                        viewCount = 156
                    },
                    new {
                        id = 2,
                        title = "Köpeklerde Kalp Kurdu Hastalığı",
                        description = "Sivrisinekler aracılığıyla bulaşan ve köpeklerin kalp ve akciğerlerini etkileyen paraziter bir hastalık.",
                        imageUrl = "/img/diseases/disease2.jpg",
                        category = "Kardiyovasküler",
                        animalType = "Köpek",
                        viewCount = 134
                    },
                    new {
                        id = 3,
                        title = "Kuşlarda Aspergilloz",
                        description = "Kuşlarda görülen, küf sporlarının solunması sonucu ortaya çıkan fungal bir solunum yolu enfeksiyonu.",
                        imageUrl = "/img/diseases/disease3.jpg",
                        category = "Solunum",
                        animalType = "Kuş",
                        viewCount = 89
                    },
                    new {
                        id = 4,
                        title = "Kedilerde İdrar Yolu Enfeksiyonu",
                        description = "Kedilerde sık görülen, idrar yaparken ağrı ve sık idrara çıkma gibi belirtilerle kendini gösteren bir rahatsızlık.",
                        imageUrl = "/img/diseases/disease4.jpg",
                        category = "Üriner",
                        animalType = "Kedi",
                        viewCount = 112
                    },
                    new {
                        id = 5,
                        title = "Köpeklerde Parvovirus",
                        description = "Özellikle yavru köpeklerde görülen, şiddetli kusma ve kanlı ishal ile seyreden bulaşıcı bir viral hastalık.",
                        imageUrl = "/img/diseases/disease5.jpg",
                        category = "Viral",
                        animalType = "Köpek",
                        viewCount = 128
                    },
                    new {
                        id = 6,
                        title = "Tavşanlarda Miksomatoz",
                        description = "Sivrisinekler ve pireler aracılığıyla bulaşan, tavşanlarda görülen ciddi bir viral hastalık.",
                        imageUrl = "/img/diseases/disease6.jpg",
                        category = "Viral",
                        animalType = "Tavşan",
                        viewCount = 76
                    }
                };
                
                return Ok(sampleData);
            }

            return Ok(featuredDiseases);
        }

        // GET: api/Diseases/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Disease>> GetDisease(int id)
        {
            var disease = await _context.Diseases.FindAsync(id);

            if (disease == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            disease.ViewCount++;
            await _context.SaveChangesAsync();

            return disease;
        }

        // GET: api/DiseasesApi/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Disease>>> SearchDiseases(string? term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return await _context.Diseases.Take(10).ToListAsync();
            }

            var diseases = await _context.Diseases
                .Where(d => d.Name.Contains(term) || d.Symptoms.Contains(term))
                .Take(10)
                .ToListAsync();

            return diseases;
        }

        // POST: api/DiseasesApi
        [HttpPost]
        public async Task<ActionResult<Disease>> PostDisease(LegacyDiseaseCreateRequest request)
        {
            var disease = new Disease
            {
                Name = request.Name.Trim(), Description = request.Description.Trim(), Symptoms = request.Symptoms?.Trim(),
                Treatments = request.Treatments?.Trim(), Treatment = request.Treatment?.Trim(), Prevention = request.Prevention?.Trim(),
                PetType = request.PetType?.Trim(), AnimalType = request.AnimalType?.Trim(),
                FeaturedImage = request.FeaturedImage?.Trim() ?? "img/hero-health-v2.png", Category = request.Category?.Trim(),
                SeverityLevel = request.SeverityLevel?.Trim(), ViewCount = 0, PublishDate = DateTime.Now
            };

            _context.Diseases.Add(disease);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDisease), new { id = disease.Id }, disease);
        }

        // DELETE: api/DiseasesApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDisease(int id)
        {
            var disease = await _context.Diseases.FindAsync(id);
            if (disease == null)
            {
                return NotFound();
            }

            _context.Diseases.Remove(disease);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
} 
