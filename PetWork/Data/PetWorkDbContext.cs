using Microsoft.EntityFrameworkCore;
using PetWork.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PetWork.Data
{
    public class PetWorkDbContext : DbContext
    {
        private readonly ILogger<PetWorkDbContext> _logger;
        
        public PetWorkDbContext(DbContextOptions<PetWorkDbContext> options, ILogger<PetWorkDbContext> logger = null)
            : base(options)
        {
            _logger = logger;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Pet> Pets { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<Disease> Diseases { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<Guide> Guides { get; set; }
        
        public override int SaveChanges()
        {
            try
            {
                var entries = ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                    .ToList();
                
                _logger?.LogInformation("SaveChanges çağrıldı: {0} değişik entity", entries.Count);
                
                if (entries.Count > 0)
                {
                    foreach (var entry in entries)
                    {
                        _logger?.LogInformation("Entity değişim: {0}, State: {1}", 
                            entry.Entity.GetType().Name, entry.State);
                    }
                }
                
                return base.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveChanges sırasında hata");
                throw;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Question - User ilişkisi
            modelBuilder.Entity<Question>()
                .HasOne(q => q.User)
                .WithMany(u => u.Questions)
                .HasForeignKey(q => q.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Answer - User ilişkisi
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.User)
                .WithMany(u => u.Answers)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Answer - Question ilişkisi
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Pet - User ilişkisi
            modelBuilder.Entity<Pet>()
                .HasOne(p => p.User)
                .WithMany(u => u.Pets)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Recipe - User ilişkisi
            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
} 