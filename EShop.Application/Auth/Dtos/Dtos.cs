using System.ComponentModel.DataAnnotations;

namespace EShop.Application.Auth.Dtos
{
    public class RegisterDto
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "密码至少6位")]
        public string Password { get; set; }

        [EmailAddress]
        public string Email { get; set; }
    }

    public class LoginDto
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; }
        public string Username { get; set; }
        public long UserId { get; set; }
    }
}