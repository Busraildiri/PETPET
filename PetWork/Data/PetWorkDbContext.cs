using Microsoft.EntityFrameworkCore;
using PetWork.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PetWork.Data
{
    public class PetWorkDbContext : DbContext
    {
        private readonly ILogger<PetWorkDbContext>? _logger;
        
        public PetWorkDbContext(DbContextOptions options, ILogger<PetWorkDbContext>? logger = null)
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

            if (Database.IsNpgsql())
            {
                modelBuilder.HasDefaultSchema(PostgresPetWorkDbContext.SchemaName);
                modelBuilder.HasPostgresExtension("citext");

                modelBuilder.Entity<User>().Property(user => user.Username).HasColumnType("citext");
                modelBuilder.Entity<User>().Property(user => user.Email).HasColumnType("citext");
                modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
                modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();

                // Kaynak SQL Server datetime2 değerleri saat dilimi taşımıyor. İlk aktarımda
                // saat kaymasını önlemek için olay zamanlarını timestamp without time zone
                // olarak koruyoruz. DateOfBirth yalnızca takvim tarihidir.
                foreach (var property in modelBuilder.Model.GetEntityTypes()
                             .SelectMany(entityType => entityType.GetProperties())
                             .Where(property => property.ClrType == typeof(DateTime) ||
                                                property.ClrType == typeof(DateTime?)))
                {
                    property.SetColumnType("timestamp without time zone");
                }

                modelBuilder.Entity<Pet>()
                    .Property(pet => pet.DateOfBirth)
                    .HasColumnType("date");

                modelBuilder.Entity<User>().Property(user => user.IsAdmin).HasDefaultValue(false);
                modelBuilder.Entity<Disease>().Property(disease => disease.ViewCount).HasDefaultValue(0);
                modelBuilder.Entity<Disease>().Property(disease => disease.PublishDate)
                    .HasDefaultValue(new DateTime(1, 1, 1));
                modelBuilder.Entity<Recipe>().Property(recipe => recipe.Description).HasDefaultValue(string.Empty);
                modelBuilder.Entity<Recipe>().Property(recipe => recipe.PreparationTime).HasDefaultValue(0);
                modelBuilder.Entity<Pet>().Property(pet => pet.PetType).HasDefaultValue(string.Empty);
            }

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
