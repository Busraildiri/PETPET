using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PetWork.Models
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
        [StringLength(50, ErrorMessage = "Kullanıcı adı en fazla 50 karakter olmalıdır.")]
        [Display(Name = "Kullanıcı Adı")]
        public string Username { get; set; }
        
        [Required(ErrorMessage = "E-posta adresi gereklidir.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }
        
        [Display(Name = "Hakkımda")]
        public string? Bio { get; set; }
        
        [Display(Name = "Profil Resmi")]
        public IFormFile? ProfileImage { get; set; }
        
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut Şifre")]
        public string? CurrentPassword { get; set; }
        
        [StringLength(100, ErrorMessage = "Şifre en az {2} karakter uzunluğunda olmalıdır.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre")]
        public string? NewPassword { get; set; }
        
        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre Onayı")]
        [Compare("NewPassword", ErrorMessage = "Şifreler eşleşmiyor.")]
        public string? ConfirmNewPassword { get; set; }
    }
} 