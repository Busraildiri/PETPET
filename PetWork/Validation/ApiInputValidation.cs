using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PetWork.Controllers.Api;

namespace PetWork.Validation;

public sealed class ApiInputValidationFilter : IActionFilter
{
    private static readonly IReadOnlyDictionary<string, int> ParameterLengths =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = 50, ["petType"] = 50, ["sortBy"] = 20, ["term"] = 80,
            ["query"] = 100, ["search"] = 100, ["input"] = 80, ["kind"] = 20, ["city"] = 80,
            ["district"] = 80, ["provider"] = 30, ["tags"] = 300, ["path"] = 500,
            ["token"] = 200
        };

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api")) return;

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is int number && IsIdentifier(name) && number <= 0)
                Add(errors, name, "Kimlik değeri sıfırdan büyük olmalıdır.");
            if (value is int take && name.Equals("take", StringComparison.OrdinalIgnoreCase) && take is < 1 or > 100)
                Add(errors, name, "Kayıt sayısı 1-100 arasında olmalıdır.");
            if (value is string text && ParameterLengths.TryGetValue(name, out var maximum) && text.Length > maximum)
                Add(errors, name, $"Değer en fazla {maximum} karakter olabilir.");
            if (value is string sortBy && name.Equals("sortBy", StringComparison.OrdinalIgnoreCase) &&
                sortBy is not ("newest" or "oldest" or "mostviewed" or "mostanswered" or "alphabetical"))
                Add(errors, name, "Geçersiz sıralama değeri.");
            ApiInputSchemas.Validate(value, name, errors);
        }

        if (errors.Count == 0) return;
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(
            errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "İstek doğrulaması başarısız."
        });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    private static bool IsIdentifier(string name) =>
        name.Equals("id", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Id", StringComparison.OrdinalIgnoreCase);

    internal static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages)) errors[field] = messages = [];
        messages.Add(message);
    }
}

internal static class ApiInputSchemas
{
    private static readonly HashSet<string> ListingStatuses = new(StringComparer.OrdinalIgnoreCase) { "active", "adopted", "closed" };
    private static readonly HashSet<string> ApplicationStatuses = new(StringComparer.OrdinalIgnoreCase) { "accepted", "rejected" };
    private static readonly HashSet<string> LostStatuses = new(StringComparer.OrdinalIgnoreCase) { "active", "resolved", "closed" };
    private static readonly HashSet<string> LostKinds = new(StringComparer.OrdinalIgnoreCase) { "lost", "found" };
    private static readonly HashSet<string> MatchPurposes = new(StringComparer.OrdinalIgnoreCase) { "friendship", "mating" };
    private static readonly HashSet<string> Platforms = new(StringComparer.OrdinalIgnoreCase) { "ios", "android" };

    public static void Validate(object? value, string argumentName, Dictionary<string, List<string>> errors)
    {
        switch (value)
        {
            case null: return;
            case LegacyBlogCreateRequest request: ValidateBlog(request, errors); break;
            case LegacyQuestionCreateRequest request: ValidateQuestion(request, errors); break;
            case LegacyRecipeCreateRequest request: ValidateRecipe(request, errors); break;
            case LegacyDiseaseCreateRequest request: ValidateDisease(request, errors); break;
            case MobileRegisterRequest request:
                Text(request.Username, "username", 3, 50, errors); Text(request.Email, "email", 3, 254, errors);
                Text(request.Password, "password", 8, 100, errors); Text(request.ConfirmPassword, "confirmPassword", 8, 100, errors);
                if (!request.AcceptTerms) ApiInputValidationFilter.Add(errors, "acceptTerms", "Üyelik koşulları kabul edilmelidir."); break;
            case MobileLoginRequest request:
                Text(request.EmailOrUsername, "emailOrUsername", 1, 254, errors); Text(request.Password, "password", 1, 100, errors); break;
            case MobileRefreshRequest request: Text(request.RefreshToken, "refreshToken", 20, 200, errors); break;
            case MobileChangePasswordRequest request:
                Text(request.CurrentPassword, "currentPassword", 1, 100, errors); Text(request.NewPassword, "newPassword", 8, 100, errors); break;
            case MobileForgotPasswordRequest request: Text(request.Email, "email", 3, 254, errors); break;
            case MobileResetPasswordRequest request:
                Text(request.Token, "token", 20, 200, errors); Text(request.NewPassword, "newPassword", 8, 100, errors); break;
            case MobileDeleteAccountRequest request:
                Text(request.Password, "password", 1, 100, errors);
                if (!string.Equals(request.Confirmation, "SİL", StringComparison.Ordinal)) ApiInputValidationFilter.Add(errors, "confirmation", "Onay değeri SİL olmalıdır."); break;
            case MobileVerifyEmailRequest request:
                Text(request.ChallengeToken, "challengeToken", 20, 200, errors);
                if (request.Code?.Length != 6 || request.Code.Any(character => !char.IsDigit(character))) ApiInputValidationFilter.Add(errors, "code", "Kod altı rakam olmalıdır."); break;
            case MobileResendEmailCodeRequest request: Text(request.ChallengeToken, "challengeToken", 20, 200, errors); break;
            case MobileCreateAdoptionListingRequest request:
                Text(request.PetName, "petName", 1, 50, errors); Text(request.Species, "species", 2, 50, errors);
                Text(request.Breed, "breed", 0, 100, errors); Range(request.AgeYears, "ageYears", 0, 40, errors);
                Text(request.Gender, "gender", 0, 20, errors); Text(request.City, "city", 2, 80, errors);
                Text(request.District, "district", 0, 80, errors); Text(request.HealthInfo, "healthInfo", 5, 500, errors);
                Text(request.Story, "story", 10, 2000, errors); Image(request.ImageBase64, "imageBase64", true, errors);
                ContentType(request.ImageContentType, true, errors); break;
            case MobileCreateAdoptionApplicationRequest request: Text(request.Message, "message", 10, 1000, errors); break;
            case MobileUpdateAdoptionApplicationRequest request: Allowed(request.Status, "status", ApplicationStatuses, errors); break;
            case MobileUpdateAdoptionListingRequest request: Allowed(request.Status, "status", ListingStatuses, errors); break;
            case MobileReportRequest request: Text(request.Reason, "reason", 3, 500, errors); break;
            case MobileCreateProductReviewRequest request:
                Text(request.Brand, "brand", 2, 100, errors); Text(request.ProductName, "productName", 2, 150, errors);
                Text(request.PetType, "petType", 2, 50, errors); Text(request.Experience, "experience", 10, 1500, errors);
                Range(request.TasteScore, "tasteScore", 1, 5, errors); Range(request.IngredientScore, "ingredientScore", 1, 5, errors);
                Range(request.DigestionScore, "digestionScore", 1, 5, errors); Range(request.ValueScore, "valueScore", 1, 5, errors);
                Image(request.ImageBase64, "imageBase64", false, errors); ContentType(request.ImageContentType, false, errors); break;
            case NearbyLocationRequest request:
                Range(request.Latitude, "latitude", -90, 90, errors); Range(request.Longitude, "longitude", -180, 180, errors);
                Range(request.RadiusMeters, "radiusMeters", 1000, 20000, errors); break;
            case MobilePushTokenRequest request:
                Text(request.Token, "token", 10, 200, errors); Text(request.Platform, "platform", 0, 20, errors);
                if (!string.IsNullOrWhiteSpace(request.Platform)) Allowed(request.Platform, "platform", Platforms, errors); break;
            case MobileCreateLostPetRequest request:
                Allowed(request.Kind, "kind", LostKinds, errors); Text(request.PetName, "petName", 1, 50, errors);
                Text(request.Species, "species", 2, 50, errors); Text(request.Breed, "breed", 0, 100, errors);
                Text(request.DistinguishingFeatures, "distinguishingFeatures", 5, 1000, errors);
                RequiredDate(request.EventAt, "eventAt", errors); Coordinates(request.Latitude, request.Longitude, errors);
                Text(request.City, "city", 2, 80, errors); Text(request.District, "district", 2, 80, errors);
                Text(request.Neighborhood, "neighborhood", 0, 120, errors); Text(request.CollarOrMicrochip, "collarOrMicrochip", 0, 500, errors);
                Text(request.Notes, "notes", 0, 1500, errors); Image(request.ImageBase64, "imageBase64", true, errors);
                ContentType(request.ImageContentType, true, errors); break;
            case MobileCreateLostPetSightingRequest request:
                Text(request.LocationLabel, "locationLabel", 3, 200, errors); Text(request.Note, "note", 0, 1000, errors);
                RequiredDate(request.SeenAt, "seenAt", errors); Coordinates(request.Latitude, request.Longitude, errors); break;
            case MobileUpdateLostPetStatusRequest request: Allowed(request.Status, "status", LostStatuses, errors); break;
            case MobileCreateSocialPostRequest request:
                Text(request.Body, "body", 2, 2000, errors); Text(request.Tags, "tags", 0, 300, errors);
                Image(request.ImageBase64, "imageBase64", false, errors); ContentType(request.ImageContentType, false, errors); break;
            case MobileCreateSocialCommentRequest request: Text(request.Body, "body", 1, 1000, errors); break;
            case MobileReportSocialPostRequest request: Text(request.Reason, "reason", 3, 500, errors); break;
            case MobileAnswerRequest request: Text(request.Content, "content", 2, 5000, errors); break;
            case MobileCreateQuestionRequest request:
                Text(request.Title, "title", 5, 200, errors); Text(request.Content, "content", 10, 4000, errors);
                Text(request.Category, "category", 1, 50, errors); break;
            case MobilePetRequest request:
                Text(request.Name, "name", 2, 50, errors); Text(request.Type, "type", 1, 30, errors);
                Text(request.Breed, "breed", 0, 80, errors); Range(request.Age, "age", 0, 80, errors);
                Text(request.Gender, "gender", 0, 20, errors); Text(request.Description, "description", 0, 500, errors); break;
            case PatiMatchEnrollRequest request:
                Range(request.PetId, "petId", 1, int.MaxValue, errors); Allowed(request.Purpose, "purpose", MatchPurposes, errors);
                Text(request.City, "city", 2, 80, errors); Text(request.District, "district", 0, 80, errors);
                if (!request.AcceptSafetyTerms) ApiInputValidationFilter.Add(errors, "acceptSafetyTerms", "Güvenlik koşulları kabul edilmelidir.");
                if (request.PreferredTypes.Count is < 1 or > 6) ApiInputValidationFilter.Add(errors, "preferredTypes", "1-6 tercih seçilmelidir.");
                foreach (var item in request.PreferredTypes) Text(item, "preferredTypes", 1, 30, errors); break;
            case PatiMatchDecisionRequest request:
                Range(request.SourcePetId, "sourcePetId", 1, int.MaxValue, errors); Range(request.TargetPetId, "targetPetId", 1, int.MaxValue, errors); break;
            case PatiMatchMessageRequest request:
                Range(request.SourcePetId, "sourcePetId", 1, int.MaxValue, errors); Range(request.TargetPetId, "targetPetId", 1, int.MaxValue, errors);
                Text(request.Body, "body", 1, 1000, errors); break;
            case MobileNotificationPreferencesRequest: break;
            case MobileToggleReactionRequest: break;
        }
    }

    private static void ValidateBlog(LegacyBlogCreateRequest r, Dictionary<string, List<string>> e)
    { Text(r.Title, "title", 1, 200, e); Text(r.Content, "content", 1, 20000, e); Text(r.Category, "category", 1, 50, e); Text(r.FeaturedImage, "featuredImage", 0, 500, e); Text(r.ImageUrl, "imageUrl", 0, 500, e); }
    private static void ValidateQuestion(LegacyQuestionCreateRequest r, Dictionary<string, List<string>> e)
    { Text(r.Title, "title", 1, 200, e); Text(r.Content, "content", 1, 10000, e); Text(r.Category, "category", 0, 50, e); Text(r.Tags, "tags", 0, 300, e); }
    private static void ValidateRecipe(LegacyRecipeCreateRequest r, Dictionary<string, List<string>> e)
    { Text(r.Title, "title", 1, 200, e); Text(r.Description, "description", 1, 4000, e); Text(r.Ingredients, "ingredients", 1, 10000, e); Text(r.Instructions, "instructions", 1, 10000, e); Text(r.Content, "content", 0, 20000, e); Text(r.PetType, "petType", 0, 50, e); Range(r.PreparationTime, "preparationTime", 1, 1440, e); Text(r.FeaturedImage, "featuredImage", 0, 500, e); Text(r.ImageUrl, "imageUrl", 0, 500, e); }
    private static void ValidateDisease(LegacyDiseaseCreateRequest r, Dictionary<string, List<string>> e)
    { Text(r.Name, "name", 1, 200, e); Text(r.Description, "description", 1, 20000, e); Text(r.Symptoms, "symptoms", 0, 10000, e); Text(r.Treatments, "treatments", 0, 10000, e); Text(r.Prevention, "prevention", 0, 10000, e); Text(r.PetType, "petType", 0, 50, e); Text(r.FeaturedImage, "featuredImage", 0, 500, e); Text(r.Category, "category", 0, 50, e); Text(r.SeverityLevel, "severityLevel", 0, 30, e); }

    private static void Text(string? value, string field, int minimum, int maximum, Dictionary<string, List<string>> errors)
    { var length = value?.Trim().Length ?? 0; if (length < minimum || length > maximum) ApiInputValidationFilter.Add(errors, field, $"Alan {minimum}-{maximum} karakter arasında olmalıdır."); }
    private static void Range(double? value, string field, double minimum, double maximum, Dictionary<string, List<string>> errors)
    { if (value.HasValue && (value.Value < minimum || value.Value > maximum)) ApiInputValidationFilter.Add(errors, field, $"Değer {minimum}-{maximum} arasında olmalıdır."); }
    private static void Range(int? value, string field, int minimum, int maximum, Dictionary<string, List<string>> errors)
    { if (value.HasValue && (value.Value < minimum || value.Value > maximum)) ApiInputValidationFilter.Add(errors, field, $"Değer {minimum}-{maximum} arasında olmalıdır."); }
    private static void Range(decimal? value, string field, decimal minimum, decimal maximum, Dictionary<string, List<string>> errors)
    { if (value.HasValue && (value.Value < minimum || value.Value > maximum)) ApiInputValidationFilter.Add(errors, field, $"Değer {minimum}-{maximum} arasında olmalıdır."); }
    private static void Allowed(string? value, string field, HashSet<string> allowed, Dictionary<string, List<string>> errors)
    { if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value.Trim())) ApiInputValidationFilter.Add(errors, field, $"Geçersiz değer. İzin verilenler: {string.Join(", ", allowed)}."); }
    private static void Image(string? value, string field, bool required, Dictionary<string, List<string>> errors)
    { if (required && string.IsNullOrWhiteSpace(value)) ApiInputValidationFilter.Add(errors, field, "Görsel gereklidir."); else if (value?.Length > 11_500_000) ApiInputValidationFilter.Add(errors, field, "Kodlanmış görsel en fazla 11.500.000 karakter olabilir."); }
    private static void ContentType(string? value, bool required, Dictionary<string, List<string>> errors)
    { if (required && string.IsNullOrWhiteSpace(value)) ApiInputValidationFilter.Add(errors, "imageContentType", "Görsel türü gereklidir."); else if (!string.IsNullOrWhiteSpace(value) && value is not ("image/jpeg" or "image/png" or "image/webp")) ApiInputValidationFilter.Add(errors, "imageContentType", "Yalnızca JPEG, PNG veya WebP kabul edilir."); }
    private static void RequiredDate(DateTime value, string field, Dictionary<string, List<string>> errors)
    { if (value == default) ApiInputValidationFilter.Add(errors, field, "Tarih gereklidir."); }
    private static void Coordinates(double? latitude, double? longitude, Dictionary<string, List<string>> errors)
    { if (latitude.HasValue != longitude.HasValue) ApiInputValidationFilter.Add(errors, "coordinates", "Enlem ve boylam birlikte gönderilmelidir."); Range(latitude, "latitude", -90, 90, errors); Range(longitude, "longitude", -180, 180, errors); }
}

public sealed record LegacyBlogCreateRequest(string Title, string Content, string Category, string? FeaturedImage, string? ImageUrl);
public sealed record LegacyQuestionCreateRequest(string Title, string Content, string? Category, string? Tags);
public sealed record LegacyRecipeCreateRequest(string Title, string Description, string Ingredients, string Instructions,
    string? Content, string? PetType, string? AnimalType, string? DietType, int PreparationTime = 30,
    string? FeaturedImage = null, string? ImageUrl = null, string? Difficulty = null, string? PrepTime = null);
public sealed record LegacyDiseaseCreateRequest(string Name, string Description, string? Symptoms, string? Treatments,
    string? Treatment, string? Prevention, string? PetType, string? AnimalType, string? FeaturedImage,
    string? Category, string? SeverityLevel);
