using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PetWork.Models;

namespace PetWork.Security;

public static class PasswordAuthentication
{
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(
        null!, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public static PasswordVerificationResult Verify(PasswordHasher<User> hasher, User? user, string password)
    {
        var result = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, password);
        return user is null ? PasswordVerificationResult.Failed : result;
    }
}
