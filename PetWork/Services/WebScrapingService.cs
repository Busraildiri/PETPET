using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using PetWork.Models;

namespace PetWork.Services
{
    public class WebScrapingService
    {
        private readonly HttpClient _httpClient;

        public WebScrapingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Wikipedia'dan hayvan hastalıkları bilgilerini çeker
        /// </summary>
        public async Task<List<Disease>> ScrapeDiseaseInfoFromWikipedia(string animalType)
        {
            var diseases = new List<Disease>();
            string url = "";

            // Hayvan türüne göre Wikipedia sayfasını belirle
            switch (animalType.ToLower())
            {
                case "dog":
                case "köpek":
                    url = "https://en.wikipedia.org/wiki/Dog_health";
                    break;
                case "cat":
                case "kedi":
                    url = "https://en.wikipedia.org/wiki/Cat_health";
                    break;
                case "bird":
                case "kuş":
                    url = "https://en.wikipedia.org/wiki/Bird_disease";
                    break;
                default:
                    url = $"https://en.wikipedia.org/wiki/{animalType}_health";
                    break;
            }

            try
            {
                string html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Wikipedia'nın hastalıkları listelediği bölümü bul
                var diseaseSections = htmlDoc.DocumentNode.SelectNodes("//h2[contains(., 'Disease')]/following-sibling::ul/li");

                if (diseaseSections != null)
                {
                    foreach (var section in diseaseSections)
                    {
                        string diseaseName = section.InnerText.Split(' ')[0].Trim();
                        string description = section.InnerText;

                        if (!string.IsNullOrEmpty(diseaseName))
                        {
                            diseases.Add(new Disease
                            {
                                Name = diseaseName,
                                Description = description,
                                AnimalType = animalType,
                                Symptoms = "Semptomlar Wikipedia'dan çekilemiyor.",
                                Treatment = "Tedavi bilgileri Wikipedia'dan çekilemiyor.",
                                Prevention = "Önleme bilgileri Wikipedia'dan çekilemiyor."
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda loglama yapılabilir
                Console.WriteLine($"Wikipedia scraping error: {ex.Message}");
            }

            return diseases;
        }

        /// <summary>
        /// Veteriner sitelerinden tarif bilgilerini çeker
        /// </summary>
        public async Task<List<Recipe>> ScrapeRecipesFromVetSites()
        {
            var recipes = new List<Recipe>();
            string url = "https://www.petnutritionalliance.org/pet-recipes/";

            try
            {
                string html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Tarif başlıklarını ve içeriklerini çek
                var recipeItems = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'recipe-item')]");

                if (recipeItems != null)
                {
                    foreach (var item in recipeItems)
                    {
                        var titleNode = item.SelectSingleNode(".//h3");
                        var descNode = item.SelectSingleNode(".//p");
                        var imageNode = item.SelectSingleNode(".//img");

                        string title = titleNode?.InnerText?.Trim() ?? "Tarif Başlığı";
                        string description = descNode?.InnerText?.Trim() ?? "Tarif açıklaması bulunamadı.";
                        string imageUrl = imageNode?.GetAttributeValue("src", "") ?? "";

                        // Basit bir hayvan türü tespiti
                        string animalType = "Genel";
                        if (title.Contains("Cat") || title.Contains("Kedi"))
                            animalType = "Kedi";
                        else if (title.Contains("Dog") || title.Contains("Köpek"))
                            animalType = "Köpek";

                        recipes.Add(new Recipe
                        {
                            Title = title,
                            Content = description,
                            AnimalType = animalType,
                            Ingredients = "İçerik bilgileri çekilemiyor.",
                            PrepTime = "30 dakika",
                            Difficulty = "Orta",
                            ImageUrl = imageUrl,
                            PublishDate = DateTime.Now,
                            ViewCount = 0,
                            UserId = 1 // Admin kullanıcı ID'si
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Hata durumunda loglama yapılabilir
                Console.WriteLine($"Recipe scraping error: {ex.Message}");
            }

            return recipes;
        }
    }
} 