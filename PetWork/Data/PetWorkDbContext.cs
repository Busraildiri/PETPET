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
        public DbSet<ExternalContentSource> ExternalContentSources { get; set; }
        public DbSet<ContentImportAudit> ContentImportAudits { get; set; }
        public DbSet<SocialPost> SocialPosts { get; set; }
        public DbSet<SocialComment> SocialComments { get; set; }
        public DbSet<SocialPostReport> SocialPostReports { get; set; }
        public DbSet<SocialPostLike> SocialPostLikes { get; set; }
        public DbSet<SocialPostSave> SocialPostSaves { get; set; }
        public DbSet<SocialCommentLike> SocialCommentLikes { get; set; }
        public DbSet<MobileAuthSession> MobileAuthSessions { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<PatiMatchProfile> PatiMatchProfiles { get; set; }
        public DbSet<PatiMatchDecision> PatiMatchDecisions { get; set; }
        public DbSet<PatiMatchMessage> PatiMatchMessages { get; set; }
        public DbSet<LostPetListing> LostPetListings { get; set; }
        public DbSet<LostPetSighting> LostPetSightings { get; set; }
        public DbSet<AdoptionListing> AdoptionListings { get; set; }
        public DbSet<AdoptionApplication> AdoptionApplications { get; set; }
        public DbSet<AdoptionListingReport> AdoptionListingReports { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<ProductReviewReport> ProductReviewReports { get; set; }
        public DbSet<MobileNotification> MobileNotifications { get; set; }
        
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

            modelBuilder.Entity<User>().HasIndex(user => user.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();

            if (Database.IsNpgsql())
            {
                modelBuilder.HasDefaultSchema(PostgresPetWorkDbContext.SchemaName);
                modelBuilder.HasPostgresExtension("citext");

                modelBuilder.Entity<User>().Property(user => user.Username).HasColumnType("citext");
                modelBuilder.Entity<User>().Property(user => user.Email).HasColumnType("citext");

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

            modelBuilder.Entity<ExternalContentSource>()
                .HasIndex(source => new { source.Provider, source.ExternalId, source.ContentType })
                .IsUnique();

            modelBuilder.Entity<ExternalContentSource>()
                .HasIndex(source => new { source.ReviewStatus, source.ImportedAt });

            modelBuilder.Entity<ExternalContentSource>()
                .HasOne(source => source.ParentSource)
                .WithMany(source => source.Children)
                .HasForeignKey(source => source.ParentSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPost>()
                .HasOne(post => post.User)
                .WithMany(user => user.SocialPosts)
                .HasForeignKey(post => post.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPost>()
                .HasIndex(post => new { post.IsDeleted, post.CreatedAt });

            modelBuilder.Entity<SocialComment>()
                .HasOne(comment => comment.SocialPost)
                .WithMany(post => post.Comments)
                .HasForeignKey(comment => comment.SocialPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialComment>()
                .HasOne(comment => comment.User)
                .WithMany(user => user.SocialComments)
                .HasForeignKey(comment => comment.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SocialComment>()
                .HasIndex(comment => new { comment.SocialPostId, comment.IsDeleted, comment.CreatedAt });

            modelBuilder.Entity<SocialPostReport>()
                .HasOne(report => report.SocialPost)
                .WithMany(post => post.Reports)
                .HasForeignKey(report => report.SocialPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPostReport>()
                .HasOne(report => report.User)
                .WithMany(user => user.SocialPostReports)
                .HasForeignKey(report => report.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SocialPostReport>()
                .HasIndex(report => new { report.SocialPostId, report.UserId })
                .IsUnique();

            modelBuilder.Entity<SocialPostReport>()
                .HasIndex(report => new { report.IsResolved, report.CreatedAt });

            modelBuilder.Entity<SocialPostLike>()
                .HasOne(like => like.SocialPost)
                .WithMany(post => post.Likes)
                .HasForeignKey(like => like.SocialPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPostLike>()
                .HasOne(like => like.User)
                .WithMany()
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPostLike>()
                .HasIndex(like => new { like.SocialPostId, like.UserId })
                .IsUnique();

            modelBuilder.Entity<SocialPostSave>()
                .HasOne(save => save.SocialPost)
                .WithMany(post => post.Saves)
                .HasForeignKey(save => save.SocialPostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPostSave>()
                .HasOne(save => save.User)
                .WithMany()
                .HasForeignKey(save => save.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialPostSave>()
                .HasIndex(save => new { save.SocialPostId, save.UserId })
                .IsUnique();

            modelBuilder.Entity<SocialCommentLike>()
                .HasOne(like => like.SocialComment)
                .WithMany(comment => comment.Likes)
                .HasForeignKey(like => like.SocialCommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialCommentLike>()
                .HasOne(like => like.User)
                .WithMany()
                .HasForeignKey(like => like.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialCommentLike>()
                .HasIndex(like => new { like.SocialCommentId, like.UserId })
                .IsUnique();

            modelBuilder.Entity<LostPetListing>()
                .HasOne(listing => listing.User)
                .WithMany()
                .HasForeignKey(listing => listing.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LostPetListing>()
                .HasIndex(listing => new { listing.IsDeleted, listing.Status, listing.ExpiresAt });

            modelBuilder.Entity<LostPetListing>()
                .HasIndex(listing => new { listing.City, listing.District, listing.CreatedAt });

            modelBuilder.Entity<LostPetSighting>()
                .HasOne(sighting => sighting.LostPetListing)
                .WithMany(listing => listing.Sightings)
                .HasForeignKey(sighting => sighting.LostPetListingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LostPetSighting>()
                .HasOne(sighting => sighting.User)
                .WithMany()
                .HasForeignKey(sighting => sighting.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LostPetSighting>()
                .HasIndex(sighting => new { sighting.LostPetListingId, sighting.IsDeleted, sighting.SeenAt });

            modelBuilder.Entity<MobileNotification>()
                .HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MobileNotification>()
                .HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt });

            modelBuilder.Entity<AdoptionListing>()
                .HasOne(listing => listing.User)
                .WithMany()
                .HasForeignKey(listing => listing.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AdoptionListing>()
                .HasIndex(listing => new { listing.IsDeleted, listing.Status, listing.CreatedAt });
            modelBuilder.Entity<AdoptionListing>()
                .HasIndex(listing => new { listing.City, listing.CreatedAt });

            modelBuilder.Entity<AdoptionApplication>()
                .HasOne(application => application.AdoptionListing)
                .WithMany(listing => listing.Applications)
                .HasForeignKey(application => application.AdoptionListingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AdoptionApplication>()
                .HasOne(application => application.User)
                .WithMany()
                .HasForeignKey(application => application.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AdoptionApplication>()
                .HasIndex(application => new { application.AdoptionListingId, application.UserId })
                .IsUnique();

            modelBuilder.Entity<AdoptionListingReport>()
                .HasOne(report => report.AdoptionListing)
                .WithMany(listing => listing.Reports)
                .HasForeignKey(report => report.AdoptionListingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AdoptionListingReport>()
                .HasOne(report => report.User)
                .WithMany()
                .HasForeignKey(report => report.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AdoptionListingReport>()
                .HasIndex(report => new { report.AdoptionListingId, report.UserId })
                .IsUnique();
            modelBuilder.Entity<AdoptionListingReport>()
                .HasIndex(report => new { report.IsResolved, report.CreatedAt });

            modelBuilder.Entity<ProductReview>()
                .HasOne(review => review.User)
                .WithMany()
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ProductReview>()
                .HasIndex(review => new { review.IsDeleted, review.CreatedAt });

            modelBuilder.Entity<ProductReviewReport>()
                .HasOne(report => report.ProductReview)
                .WithMany(review => review.Reports)
                .HasForeignKey(report => report.ProductReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ProductReviewReport>()
                .HasOne(report => report.User)
                .WithMany()
                .HasForeignKey(report => report.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ProductReviewReport>()
                .HasIndex(report => new { report.ProductReviewId, report.UserId })
                .IsUnique();
            modelBuilder.Entity<ProductReviewReport>()
                .HasIndex(report => new { report.IsResolved, report.CreatedAt });

            modelBuilder.Entity<PatiMatchProfile>()
                .HasOne(profile => profile.Pet)
                .WithOne()
                .HasForeignKey<PatiMatchProfile>(profile => profile.PetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PatiMatchProfile>()
                .HasIndex(profile => profile.PetId)
                .IsUnique();

            modelBuilder.Entity<PatiMatchProfile>()
                .HasIndex(profile => new { profile.IsActive, profile.City });

            modelBuilder.Entity<PatiMatchDecision>()
                .HasOne(decision => decision.SourcePet)
                .WithMany()
                .HasForeignKey(decision => decision.SourcePetId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PatiMatchDecision>()
                .HasOne(decision => decision.TargetPet)
                .WithMany()
                .HasForeignKey(decision => decision.TargetPetId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PatiMatchDecision>()
                .HasIndex(decision => new { decision.SourcePetId, decision.TargetPetId })
                .IsUnique();

            modelBuilder.Entity<PatiMatchMessage>()
                .HasOne(message => message.PetOne)
                .WithMany()
                .HasForeignKey(message => message.PetOneId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PatiMatchMessage>()
                .HasOne(message => message.PetTwo)
                .WithMany()
                .HasForeignKey(message => message.PetTwoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PatiMatchMessage>()
                .HasOne(message => message.SenderUser)
                .WithMany()
                .HasForeignKey(message => message.SenderUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PatiMatchMessage>()
                .HasIndex(message => new { message.PetOneId, message.PetTwoId, message.CreatedAt });

            modelBuilder.Entity<ExternalContentSource>()
                .HasOne(source => source.ReviewedByUser)
                .WithMany()
                .HasForeignKey(source => source.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ContentImportAudit>()
                .HasOne(audit => audit.ExternalContentSource)
                .WithMany(source => source.AuditEntries)
                .HasForeignKey(audit => audit.ExternalContentSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContentImportAudit>()
                .HasOne(audit => audit.PerformedByUser)
                .WithMany()
                .HasForeignKey(audit => audit.PerformedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MobileAuthSession>()
                .HasOne(session => session.User)
                .WithMany(user => user.MobileAuthSessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MobileAuthSession>()
                .HasIndex(session => session.RefreshTokenHash)
                .IsUnique();

            modelBuilder.Entity<MobileAuthSession>()
                .HasIndex(session => new { session.UserId, session.RevokedAt, session.RefreshExpiresAt });

            if (Database.IsNpgsql())
            {
                modelBuilder.Entity<MobileAuthSession>().Property(session => session.CreatedAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<MobileAuthSession>().Property(session => session.LastUsedAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<MobileAuthSession>().Property(session => session.AccessExpiresAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<MobileAuthSession>().Property(session => session.RefreshExpiresAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<MobileAuthSession>().Property(session => session.RevokedAt).HasColumnType("timestamp with time zone");
            }

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(token => token.User)
                .WithMany(user => user.PasswordResetTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(token => token.TokenHash)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(token => new { token.UserId, token.UsedAt, token.ExpiresAt });

            if (Database.IsNpgsql())
            {
                modelBuilder.Entity<PasswordResetToken>().Property(token => token.CreatedAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<PasswordResetToken>().Property(token => token.ExpiresAt).HasColumnType("timestamp with time zone");
                modelBuilder.Entity<PasswordResetToken>().Property(token => token.UsedAt).HasColumnType("timestamp with time zone");
            }
        }
    }
} 
