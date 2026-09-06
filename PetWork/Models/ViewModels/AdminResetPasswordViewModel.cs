using System.ComponentModel.DataAnnotations;

namespace PetWork.Models.ViewModels;

public class AdminResetPasswordViewModel
{
    [Required]
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gereklidir.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az {2} karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni geçici şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre onayı gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifreyi tekrar girin")]
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
