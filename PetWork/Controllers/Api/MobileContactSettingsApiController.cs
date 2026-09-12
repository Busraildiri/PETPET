using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/mobile/contact-settings")]
public sealed partial class MobileContactSettingsApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;

    public MobileContactSettingsApiController(PetWorkDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<MobileContactSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var user = await _context.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return Unauthorized();

        var preference = await _context.MobileContactPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return Ok(preference is null
            ? new MobileContactSettingsResponse(null, user.Email, false, false, false, false)
            : ToResponse(preference, true));
    }

    [HttpPut]
    public async Task<ActionResult<MobileContactSettingsResponse>> Update(
        MobileContactSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var phone = Clean(request.Phone);
        var email = Clean(request.Email)?.ToLowerInvariant();
        if (phone is not null && !PhonePattern().IsMatch(phone))
            return BadRequest(new { message = "Geçerli bir telefon numarası girmelisin." });
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            return BadRequest(new { message = "Geçerli bir e-posta adresi girmelisin." });

        var preference = await _context.MobileContactPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new MobileContactPreference { UserId = userId };
            _context.MobileContactPreferences.Add(preference);
        }

        preference.Phone = phone;
        preference.ContactEmail = email;
        preference.AllowPatiMatchSharing = request.AllowPatiMatchSharing;
        preference.AllowAdoptionSharing = request.AllowAdoptionSharing;
        preference.AllowLostPetSharing = request.AllowLostPetSharing;
        preference.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(preference, true));
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static MobileContactSettingsResponse ToResponse(MobileContactPreference preference, bool configured) => new(
        preference.Phone, preference.ContactEmail, preference.AllowPatiMatchSharing,
        preference.AllowAdoptionSharing, preference.AllowLostPetSharing, configured);

    [GeneratedRegex(@"^(?=(?:\D*\d){7,15}\D*$)[0-9+() .-]{7,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}

public sealed class MobileContactSettingsRequest
{
    [StringLength(30)] public string? Phone { get; init; }
    [StringLength(254)] public string? Email { get; init; }
    public bool AllowPatiMatchSharing { get; init; }
    public bool AllowAdoptionSharing { get; init; }
    public bool AllowLostPetSharing { get; init; }
}

public sealed record MobileContactSettingsResponse(string? Phone, string? Email,
    bool AllowPatiMatchSharing, bool AllowAdoptionSharing, bool AllowLostPetSharing, bool IsConfigured);
