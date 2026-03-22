using System.ComponentModel.DataAnnotations;

namespace ScrivenerSync.Web.Models;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class AcceptInvitationViewModel
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be at least 2 characters.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class InvitationExpiredViewModel
{
    public string Reason { get; set; } = string.Empty;
}
