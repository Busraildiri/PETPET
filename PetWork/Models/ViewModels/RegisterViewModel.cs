using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
        [StringLength(50, ErrorMessage = "Kullanıcı adı en fazla 50 karakter olmalıdır.")]
        [Display(Name = "Kullanıcı Adı")]
        public string Username { get; set; }
        
        [Required(ErrorMessage = "E-posta adresi gereklidir.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }
        
        [Required(ErrorMessage = "Şifre gereklidir.")]
        [StringLength(100, ErrorMessage = "Şifre en az {2} karakter uzunluğunda olmalıdır.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }
        
        [DataType(DataType.Password)]
        [Display(Name = "Şifre Onayı")]
        [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
        public string ConfirmPassword { get; set; }
        
        [Display(Name = "Kullanım Koşullarını Kabul Ediyorum")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Devam etmek için kullanım koşullarını kabul etmelisiniz.")]
        public bool AcceptTerms { get; set; }
    }
} 