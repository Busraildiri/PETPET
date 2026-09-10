using System.Security.Cryptography;

namespace PetWork.Security;

public static class ErrorReferenceCode
{
    public static string Create(HttpContext context)
    {
        var code = $"ERR-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";
        context.Response.Headers["X-Error-Reference"] = code;
        return code;
    }

    public static string UserMessage(string message, string referenceCode) =>
        $"{message} Referans kodu: {referenceCode}";
}
