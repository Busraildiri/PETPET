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
    [Route("api/recipes")]
    [ApiController]
    public class RecipesApiController : ControllerBase
    {
        private readonly PetWorkDbContext _context;

        public RecipesApiController(PetWorkDbContext context)
        {
            _context = context;
        }

        // GET: api/Recipes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Recipe>>> GetRecipes(string petType = null, string sortBy = "newest")
        {
            var recipes = _context.Recipes
                .Include(r => r.User)
                .AsQueryable();

            // Evcil hayvan türü filtresi
            if (!string.IsNullOrEmpty(petType))
            {
                recipes = recipes.Where(r => r.PetType == petType);
            }

            // Sıralama
            recipes = sortBy switch
            {
                "oldest" => recipes.OrderBy(r => r.PublishDate),
                "mostviewed" => recipes.OrderByDescending(r => r.ViewCount),
                _ => recipes.OrderByDescending(r => r.PublishDate)
            };

            return await recipes.ToListAsync();
        }

        // GET: api/Recipes/featured
        [HttpGet("featured")]
        public async Task<ActionResult<IEnumerable<object>>> GetFeaturedRecipes()
        {
            var featuredRecipes = await _context.Recipes
                .OrderByDescending(r => r.ViewCount)
                .Take(6)
                .Select(r => new
                {
                    id = r.Id,
                    title = r.Title,
                    description = r.Description,
                    imageUrl = r.ImageUrl,
                    category = r.PetType ?? "Genel",
                    animalType = r.AnimalType,
                    viewCount = r.ViewCount
                })
                .ToListAsync();
                
            return Ok(featuredRecipes);
        }

        // GET: api/Recipes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Recipe>> GetRecipe(int id)
        {
            var recipe = await _context.Recipes
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            recipe.ViewCount++;
            await _context.SaveChangesAsync();

            return recipe;
        }

        // POST: api/RecipesApi
        [HttpPost]
        public async Task<ActionResult<Recipe>> PostRecipe(Recipe recipe)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            recipe.PublishDate = DateTime.Now;
            recipe.ViewCount = 0;

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, recipe);
        }

        // DELETE: api/RecipesApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null)
            {
                return NotFound();
            }

            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
} 
