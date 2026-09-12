using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Tests;

public sealed class CommunityApiTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;
    public CommunityApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Questions_require_active_session_and_reject_whitespace_content()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var userA = await Register($"questionA_{id}", $"question-a-{id}@example.com");
        var userB = await Register($"questionB_{id}", $"question-b-{id}@example.com");

        var whitespaceQuestion = await Authorized(HttpMethod.Post, "api/mobile/questions", userA.Token,
            new { title = "     ", content = "          ", category = "Bakım" });
        Assert.Equal(HttpStatusCode.BadRequest, whitespaceQuestion.StatusCode);

        var created = await Authorized(HttpMethod.Post, "api/mobile/questions", userA.Token,
            new { title = "Kedimin bakımı", content = "Tüy bakımını nasıl düzenlemeliyim?", category = "Bakım" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var question = await created.Content.ReadFromJsonAsync<QuestionDto>();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync($"api/mobile/questions/{question!.Id}/answers", new { content = "Geçerli yanıt" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Post, $"api/mobile/questions/{question.Id}/answers", userB.Token, new { content = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await Authorized(HttpMethod.Post, $"api/mobile/questions/{question.Id}/answers", userB.Token, new { content = "Haftada bir tarayabilirsin." })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await Authorized(HttpMethod.Post, "api/mobile/auth/logout", userB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Authorized(HttpMethod.Post, $"api/mobile/questions/{question.Id}/answers", userB.Token, new { content = "İptal edilmiş token yanıtı" })).StatusCode);
    }

    [Fact]
    public async Task Social_posts_enforce_ownership_duplicate_reports_and_trimmed_validation()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var userA = await Register($"socialA_{id}", $"social-a-{id}@example.com");
        var userB = await Register($"socialB_{id}", $"social-b-{id}@example.com");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Post, "api/mobile/social/posts", userA.Token, new { body = "  " })).StatusCode);
        var post = await CreatePost(userB.Token, "Bugün veteriner kontrolümüz vardı.");
        var secondPost = await CreatePost(userB.Token, "Parkta güzel bir yürüyüş yaptık.");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authorized(HttpMethod.Delete, $"api/mobile/social/posts/{post.Id}", userA.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Post, $"api/mobile/social/posts/{post.Id}/comments", userA.Token, new { body = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await Authorized(HttpMethod.Post, $"api/mobile/social/posts/{post.Id}/comments", userA.Token, new { body = "Geçmiş olsun!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Post, $"api/mobile/social/posts/{secondPost.Id}/report", userA.Token, new { reason = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Authorized(HttpMethod.Post, $"api/mobile/social/posts/{post.Id}/report", userA.Token, new { reason = "Yanıltıcı içerik" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Authorized(HttpMethod.Post, $"api/mobile/social/posts/{post.Id}/report", userA.Token, new { reason = "Tekrar" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Authorized(HttpMethod.Delete, $"api/mobile/social/posts/{post.Id}", userB.Token)).StatusCode);
    }

    [Fact]
    public async Task Pets_reject_whitespace_names_at_the_api_boundary()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var user = await Register($"pet_{id}", $"pet-{id}@example.com");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Post, "api/mobile/pets", user.Token, new { name = "  ", type = "Kedi" })).StatusCode);
    }

    [Fact]
    public async Task Adoption_reports_reject_the_listing_owner_but_allow_another_user()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var owner = await Register($"adoptionOwner_{id}", $"adoption-owner-{id}@example.com");
        var reporter = await Register($"adoptionReporter_{id}", $"adoption-reporter-{id}@example.com");

        int listingId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            var listing = new AdoptionListing
            {
                UserId = owner.UserId,
                PetName = "Misket",
                Species = "Kedi",
                City = "İstanbul",
                HealthInfo = "Kontrolleri tamamlandı.",
                Story = "Güvenli bir yuva arıyor.",
                ImagePath = "/uploads/adoption/test.jpg"
            };
            db.AdoptionListings.Add(listing);
            await db.SaveChangesAsync();
            listingId = listing.Id;
        }

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authorized(HttpMethod.Post, $"api/mobile/adoption/listings/{listingId}/reports", owner.Token,
                new { reason = "Kendi ilanımı bildiriyorum" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Authorized(HttpMethod.Post, $"api/mobile/adoption/listings/{listingId}/reports", reporter.Token,
                new { reason = "Şüpheli iletişim bilgisi" })).StatusCode);
    }

    [Fact]
    public async Task Listing_contact_methods_are_stored_per_listing_and_disabled_in_app_actions_are_rejected()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var owner = await Register($"contactOwner_{id}", $"contact-owner-{id}@example.com");
        var visitor = await Register($"contactVisitor_{id}", $"contact-visitor-{id}@example.com");
        const string image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        var withoutPermission = await Authorized(HttpMethod.Post, "api/mobile/adoption/listings", owner.Token, new
        {
            petName = "Misket", species = "Kedi", city = "İstanbul", healthInfo = "Sağlığı iyi.",
            story = "Kalıcı ve güvenli bir yuva arıyor.", imageBase64 = image, imageContentType = "image/png",
            allowInAppMessages = false, sharePhone = true, shareEmail = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, withoutPermission.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Authorized(HttpMethod.Put, "api/mobile/contact-settings", owner.Token, new
        {
            phone = "+90 555 444 33 22", email = $"listing-{id}@example.com", allowPatiMatchSharing = false,
            allowAdoptionSharing = true, allowLostPetSharing = true
        })).StatusCode);

        var adoptionResponse = await Authorized(HttpMethod.Post, "api/mobile/adoption/listings", owner.Token, new
        {
            petName = "Misket", species = "Kedi", city = "İstanbul", healthInfo = "Sağlığı iyi.",
            story = "Kalıcı ve güvenli bir yuva arıyor.", imageBase64 = image, imageContentType = "image/png",
            allowInAppMessages = false, sharePhone = true, shareEmail = false
        });
        Assert.Equal(HttpStatusCode.Created, adoptionResponse.StatusCode);
        var adoption = (await adoptionResponse.Content.ReadFromJsonAsync<ListingContactDto>())!;
        Assert.False(adoption.AllowInAppMessages);
        Assert.Equal("+90 555 444 33 22", adoption.ContactPhone);
        Assert.Null(adoption.ContactEmail);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Authorized(HttpMethod.Post, $"api/mobile/adoption/listings/{adoption.Id}/applications", visitor.Token,
                new { message = "Bu pati için güvenli bir yuva sunabilirim." })).StatusCode);

        var lostResponse = await Authorized(HttpMethod.Post, "api/mobile/lost-pets", owner.Token, new
        {
            kind = "lost", petName = "Boncuk", species = "Kedi", distinguishingFeatures = "Kırmızı tasması var.",
            eventAt = DateTime.Now.AddMinutes(-5), city = "İstanbul", district = "Ataşehir",
            imageBase64 = image, imageContentType = "image/png", allowInAppMessages = true,
            sharePhone = false, shareEmail = true
        });
        Assert.Equal(HttpStatusCode.Created, lostResponse.StatusCode);
        var lost = (await lostResponse.Content.ReadFromJsonAsync<ListingContactDto>())!;
        Assert.True(lost.AllowInAppMessages);
        Assert.Null(lost.ContactPhone);
        Assert.Equal($"listing-{id}@example.com", lost.ContactEmail);

        Assert.Equal(HttpStatusCode.OK, (await Authorized(HttpMethod.Put, "api/mobile/contact-settings", owner.Token, new
        {
            phone = "+90 555 444 33 22", email = $"listing-{id}@example.com", allowPatiMatchSharing = false,
            allowAdoptionSharing = false, allowLostPetSharing = false
        })).StatusCode);
        var adoptionList = (await (await Authorized(HttpMethod.Get, "api/mobile/adoption/listings", visitor.Token))
            .Content.ReadFromJsonAsync<List<ListingContactDto>>())!;
        var hiddenAdoption = adoptionList.Single(item => item.Id == adoption.Id);
        Assert.Null(hiddenAdoption.ContactPhone);
        Assert.Null(hiddenAdoption.ContactEmail);
        var hiddenLost = (await (await Authorized(HttpMethod.Get, $"api/mobile/lost-pets/{lost.Id}", visitor.Token))
            .Content.ReadFromJsonAsync<ListingContactDto>())!;
        Assert.Null(hiddenLost.ContactPhone);
        Assert.Null(hiddenLost.ContactEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
        var storedAdoption = await db.AdoptionListings.FindAsync(adoption.Id);
        var storedLost = await db.LostPetListings.FindAsync(lost.Id);
        Assert.Equal("+90 555 444 33 22", storedAdoption!.ContactPhone);
        Assert.Equal($"listing-{id}@example.com", storedLost!.ContactEmail);
    }

    private async Task<AuthDto> Register(string username, string email)
    {
        var response = await _client.PostAsJsonAsync("api/mobile/auth/register", new { username, email, password = "ValidPass1!", confirmPassword = "ValidPass1!", acceptTerms = true, rememberMe = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var challenge = (await response.Content.ReadFromJsonAsync<EmailChallengeDto>())!;
        var code = _factory.VerificationSender.GetCode(challenge.ChallengeToken);
        var verified = await _client.PostAsJsonAsync("api/mobile/auth/verify-email", new { challengeToken = challenge.ChallengeToken, code });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        return (await verified.Content.ReadFromJsonAsync<AuthDto>())!;
    }
    private async Task<PostDto> CreatePost(string token, string body)
    {
        var response = await Authorized(HttpMethod.Post, "api/mobile/social/posts", token, new { body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PostDto>())!;
    }
    private async Task<HttpResponseMessage> Authorized(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }
    private sealed record AuthDto(int UserId, string Username, string Token, string RefreshToken);
    private sealed record EmailChallengeDto(string ChallengeToken);
    private sealed record QuestionDto(int Id);
    private sealed record PostDto(int Id);
    private sealed record ListingContactDto(int Id, bool AllowInAppMessages, string? ContactPhone, string? ContactEmail);
}
