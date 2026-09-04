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
    [Route("api/questions")]
    [ApiController]
    public class QuestionsApiController : ControllerBase
    {
        private readonly PetWorkDbContext _context;

        public QuestionsApiController(PetWorkDbContext context)
        {
            _context = context;
        }

        // GET: api/QuestionsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Question>>> GetQuestions(string category = null, string sortBy = "newest")
        {
            var questions = _context.Questions
                .Include(q => q.User)
                .Include(q => q.Answers)
                .AsQueryable();

            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                questions = questions.Where(q => q.Category == category);
            }

            // Sıralama uygula
            questions = sortBy switch
            {
                "oldest" => questions.OrderBy(q => q.CreatedDate),
                "mostviewed" => questions.OrderByDescending(q => q.ViewCount),
                "mostanswered" => questions.OrderByDescending(q => q.Answers.Count),
                _ => questions.OrderByDescending(q => q.CreatedDate)
            };

            return await questions.ToListAsync();
        }
        
        // GET: api/questions/latest
        [HttpGet("latest")]
        public async Task<ActionResult<IEnumerable<object>>> GetLatestQuestions()
        {
            var latestQuestions = await _context.Questions
                .Include(q => q.User)
                .Include(q => q.Answers)
                .OrderByDescending(q => q.CreatedDate)
                .Take(6)
                .Select(q => new
                {
                    id = q.Id,
                    title = q.Title,
                    content = q.Content,
                    createdAt = q.CreatedDate,
                    viewCount = q.ViewCount,
                    answersCount = q.Answers.Count,
                    user = new
                    {
                        username = q.User.Username,
                        profileImage = q.User.ProfileImage
                    },
                    petType = q.Category ?? "Genel"
                })
                .ToListAsync();

            // If no questions exist, return sample data
            if (latestQuestions == null || !latestQuestions.Any())
            {
                var sampleData = new List<object>
                {
                    new {
                        id = 1,
                        title = "Kedim çok tüy döküyor, ne yapabilirim?",
                        content = "3 yaşındaki British Shorthair kedim son zamanlarda çok fazla tüy dökmeye başladı. Nasıl beslenmeli ve nasıl bakım yapmalıyım?",
                        createdAt = DateTime.Now.AddDays(-2),
                        viewCount = 45,
                        answersCount = 3,
                        user = new {
                            username = "kediSever",
                            profileImage = "/img/user1.jpg"
                        },
                        petType = "Kedi"
                    },
                    new {
                        id = 2,
                        title = "Köpeğimin mamasını değiştirmek istiyorum, önerileriniz nedir?",
                        content = "Golden retriever cinsi köpeğim var ve daha sağlıklı bir mamaya geçmek istiyorum. Hangi markaları önerirsiniz?",
                        createdAt = DateTime.Now.AddDays(-3),
                        viewCount = 32,
                        answersCount = 5,
                        user = new {
                            username = "goldenSahibi",
                            profileImage = "/img/user2.jpg"
                        },
                        petType = "Köpek"
                    },
                    new {
                        id = 3,
                        title = "Kuşum tüylerini yoluyor, ne yapmalıyım?",
                        content = "Muhabbet kuşum son zamanlarda tüylerini yolmaya başladı. Veterinere götürdüm ve fiziksel bir sorun bulamadı. Psikolojik olabilir mi?",
                        createdAt = DateTime.Now.AddDays(-5),
                        viewCount = 28,
                        answersCount = 2,
                        user = new {
                            username = "kuşSever",
                            profileImage = "/img/user3.jpg"
                        },
                        petType = "Kuş"
                    },
                    new {
                        id = 4,
                        title = "Balıklarım için en uygun filtre hangisi?",
                        content = "60 litrelik bir akvaryumum var ve içinde 10 adet lepistes balığı bulunuyor. Hangi filtre sistemini önerirsiniz?",
                        createdAt = DateTime.Now.AddDays(-7),
                        viewCount = 18,
                        answersCount = 4,
                        user = new {
                            username = "akvaryumcu",
                            profileImage = "/img/user1.jpg"
                        },
                        petType = "Balık"
                    },
                    new {
                        id = 5,
                        title = "Tavşanım için uygun kafes boyutu ne olmalı?",
                        content = "Yeni bir tavşan sahiplendim ve mevcut kafesi biraz küçük geliyor. İdeal kafes boyutu ne olmalı?",
                        createdAt = DateTime.Now.AddDays(-8),
                        viewCount = 22,
                        answersCount = 3,
                        user = new {
                            username = "tavşancı",
                            profileImage = "/img/user2.jpg"
                        },
                        petType = "Tavşan"
                    },
                    new {
                        id = 6,
                        title = "Köpeğimin aşı takvimi nasıl olmalı?",
                        content = "2 aylık bir yavru köpek sahiplendim. Aşı takvimi hakkında bilgi almak istiyorum. Hangi aşıları ne zaman yaptırmalıyım?",
                        createdAt = DateTime.Now.AddDays(-10),
                        viewCount = 56,
                        answersCount = 7,
                        user = new {
                            username = "yeniKöpekSahibi",
                            profileImage = "/img/user3.jpg"
                        },
                        petType = "Köpek"
                    }
                };
                
                return Ok(sampleData);
            }

            return Ok(latestQuestions);
        }

        // GET: api/QuestionsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.User)
                .Include(q => q.Answers)
                .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            question.ViewCount++;
            await _context.SaveChangesAsync();

            return question;
        }

        // POST: api/QuestionsApi
        [HttpPost]
        public async Task<ActionResult<Question>> PostQuestion(Question question)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            question.CreatedDate = DateTime.Now;
            question.ViewCount = 0;

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetQuestion), new { id = question.Id }, question);
        }

        // DELETE: api/QuestionsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null)
            {
                return NotFound();
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
} 
