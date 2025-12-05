using System.ComponentModel.DataAnnotations;

namespace EShop.Application.Auth.Dtos
{
    public class ExternalLoginDto
    {
        [Required]
        public string Provider { get; set; } // "WeChat"

        [Required]
        public string OpenId { get; set; }   // 真实场景前端通常传 Code，我们为了演示简单直接传 OpenId

        [Required]
        public string Code { get; set; } // 👈 改成 Code (临时票据)
    }
}