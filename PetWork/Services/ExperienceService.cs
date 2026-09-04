using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services
{
    public class ExperienceService
    {
        private readonly PetWorkDbContext _context;

        public ExperienceService(PetWorkDbContext context)
        {
            _context = context;
        }

        // Kullanıcıya deneyim puanı ekleme
        public async Task AddExperienceAsync(int userId, int amount, string reason)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("Kullanıcı bulunamadı");

            user.ExperiencePoints += amount;
            
            // Yapılan aktivitenin kaydını tutma
            // Burada ExperienceLog gibi bir model oluşturup, kullanıcının aktivitelerini kaydedebilirsiniz
            
            // Rozet kontrolü
            await CheckAndAwardBadgesAsync(user);
            
            await _context.SaveChangesAsync();
        }
        
        // Rozet kontrolü ve ödüllendirme
        private async Task CheckAndAwardBadgesAsync(User user)
        {
            // Kullanıcının mevcut rozetlerini yükle
            await _context.Entry(user).Collection(u => u.Badges).LoadAsync();
            
            // Veritabanında tanımlı tüm rozetleri getir
            var allBadges = await _context.Badges.ToListAsync();
            
            // Deneyim puanına göre hak kazanılan rozetler
            var experienceBadges = new Dictionary<int, string>
            {
                { 100, "Yeni Başlayan" },
                { 500, "Aktif Üye" },
                { 1000, "Deneyimli Üye" },
                { 5000, "Uzman Üye" },
                { 10000, "Guru" }
            };
            
            foreach (var badge in experienceBadges)
            {
                if (user.ExperiencePoints >= badge.Key)
                {
                    // Kullanıcının bu rozeti var mı diye kontrol et
                    var existingBadge = user.Badges?.FirstOrDefault(b => b.Name == badge.Value);
                    
                    if (existingBadge == null)
                    {
                        // Veritabanında bu isimde rozet var mı diye kontrol et
                        var dbBadge = allBadges.FirstOrDefault(b => b.Name == badge.Value);
                        
                        if (dbBadge == null)
                        {
                            // Rozet yoksa oluştur
                            dbBadge = new Badge
                            {
                                Name = badge.Value,
                                Description = $"{badge.Key} deneyim puanı kazanarak {badge.Value} rozetini elde ettiniz!",
                                IconUrl = $"img/badges/{badge.Value.ToLower().Replace(" ", "-")}.png"
                            };
                            
                            _context.Badges.Add(dbBadge);
                            await _context.SaveChangesAsync();
                        }
                        
                        // Kullanıcıya rozeti ekle
                        user.Badges ??= new List<Badge>();
                        user.Badges.Add(dbBadge);
                    }
                }
            }
        }
        
        // Deneyim puanı gerektiren aktiviteler için puan değerleri
        public static class ExperiencePoints
        {
            public const int AskQuestion = 10;
            public const int AnswerQuestion = 15;
            public const int AcceptedAnswer = 25;
            public const int CreateRecipe = 30;
            public const int CreateGuide = 50;
            public const int DailyLogin = 5;
            public const int ProfileCompletion = 20;
            public const int AddPet = 15;
        }
    }
} 