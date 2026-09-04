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
    [Route("api/blogs")]
    [ApiController]
    public class BlogsApiController : ControllerBase
    {
        private readonly PetWorkDbContext _context;

        public BlogsApiController(PetWorkDbContext context)
        {
            _context = context;
        }

        // GET: api/BlogsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BlogPost>>> GetBlogs(string? category = null, string? sortBy = "newest")
        {
            var blogs = _context.BlogPosts
                .Include(b => b.User)
                .AsQueryable();

            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                blogs = blogs.Where(b => b.Category == category);
            }

            // Sıralama uygula
            blogs = sortBy switch
            {
                "oldest" => blogs.OrderBy(b => b.PublishDate),
                "mostviewed" => blogs.OrderByDescending(b => b.ViewCount),
                _ => blogs.OrderByDescending(b => b.PublishDate)
            };

            return await blogs.ToListAsync();
        }

        // GET: api/BlogsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BlogPost>> GetBlog(int id)
        {
            var blog = await _context.BlogPosts
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (blog == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            blog.ViewCount++;
            await _context.SaveChangesAsync();

            return blog;
        }

        // POST: api/BlogsApi
        [HttpPost]
        public async Task<ActionResult<BlogPost>> PostBlog(BlogPost blog)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            blog.PublishDate = DateTime.Now;
            blog.ViewCount = 0;

            _context.BlogPosts.Add(blog);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBlog), new { id = blog.Id }, blog);
        }

        // DELETE: api/BlogsApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            var blog = await _context.BlogPosts.FindAsync(id);
            if (blog == null)
            {
                return NotFound();
            }

            _context.BlogPosts.Remove(blog);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
} 
