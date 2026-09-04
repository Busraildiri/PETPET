using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı veya e-posta gereklidir.")]
        [Display(Name = "Kullanıcı Adı veya E-posta")]
        public string UsernameOrEmail { get; set; }
        
        [Required(ErrorMessage = "Şifre gereklidir.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }
        
        [Display(Name = "Beni Hatırla")]
        public bool RememberMe { get; set; }
    }
} 