using System.ComponentModel.DataAnnotations;

namespace DraftView.Web.Models;

public class LoginViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class AcceptInvitationViewModel
{
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a username.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Username may only contain letters, numbers, dots, underscores, and hyphens.")]
    public string Username { get; set; } = string.Empty;

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

public class ForgotPasswordViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class SettingsViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAuthor { get; set; }
    public bool IsReader { get; set; }
    public string? DropboxStatus { get; set; }
    public DateTime? DropboxAuthorisedAt { get; set; }
    public string DisplayTheme { get; set; } = "Light";
    public string ProseFont { get; set; } = "SystemSerif";
    public string ProseFontSize { get; set; } = "Medium";
    public string? ReaderBio { get; set; }
    public string? ReaderGenreInterests { get; set; }
    public string? ReaderPace { get; set; }
    public bool ShowDiffOnRevisit { get; set; }
    public string ReadingStyle { get; set; } = "StoryReader";
    public int DiffCooldownHours { get; set; } = 24;
}

public class ChangeDiffPreferencesViewModel
{
    public bool ShowDiffOnRevisit { get; set; }

    [Required(ErrorMessage = "Please select a reading style.")]
    public string ReadingStyle { get; set; } = "StoryReader";

    public int DiffCooldownHours { get; set; } = 24;
}

public class UpdateReaderProfileViewModel
{
    [StringLength(1000)]
    public string? ReaderBio { get; set; }

    [StringLength(500)]
    public string? ReaderGenreInterests { get; set; }

    public string? ReaderPace { get; set; }
}

public class ChangeDisplayThemeViewModel
{
    [Required(ErrorMessage = "Please select a theme.")]
    public string DisplayTheme { get; set; } = "Light";
}



public class ChangeProseFontPreferencesViewModel
{
    [Required(ErrorMessage = "Please select a font.")]
    public string ProseFont { get; set; } = "SystemSerif";

    [Required(ErrorMessage = "Please select a font size.")]
    public string ProseFontSize { get; set; } = "Medium";
}

public class ChangeDisplayNameViewModel
{
    [Required(ErrorMessage = "Please enter a display name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be at least 2 characters.")]
    public string DisplayName { get; set; } = string.Empty;
}

public class ChangeEmailViewModel
{
    [Required(ErrorMessage = "Please enter an email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please enter your current password.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Please enter your current password.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please enter a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please confirm your new password.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
public class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please choose a password.")]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8,
        ErrorMessage = "Password must be at least 8 characters.")]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please confirm your password.")]
    [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
    [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AccessDeniedViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public string ActionController { get; set; } = string.Empty;
    public string ActionAction { get; set; } = string.Empty;
}
