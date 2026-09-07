using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    public PasswordPolicyAttribute()
        : base("Şifre en az 8 karakter olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string password) return false;
        return password.Length is >= 8 and <= 100
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(character => !char.IsLetterOrDigit(character));
    }
}
