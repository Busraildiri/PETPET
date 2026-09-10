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
    [Route("api/questions")]
    [ApiController]
    public class QuestionsApiController : ControllerBase
    {
        private static readonly string[] DemoUsernames = { "kediSever", "goldenSahibi", "kusSever" };
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
                .Include(q => q.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
                .Where(q => q.User == null || !DemoUsernames.Contains(q.User.Username))
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
                .Include(q => q.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
                .Where(q => q.User == null || !DemoUsernames.Contains(q.User.Username))
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

            return Ok(latestQuestions);
        }

        // GET: api/QuestionsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(int id)
        {
            var question = await _context.Questions
                .Include(q => q.User)
                .Where(q => q.User == null || !DemoUsernames.Contains(q.User.Username))
                .Include(q => q.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
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
        public async Task<ActionResult<Question>> PostQuestion(LegacyQuestionCreateRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized();
            var question = new Question
            {
                Title = request.Title.Trim(), Content = request.Content.Trim(), Category = request.Category?.Trim(),
                Tags = request.Tags?.Trim(), CreatedDate = DateTime.Now, ViewCount = 0, UserId = userId.Value
            };

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
