using System.ComponentModel.DataAnnotations;

namespace DraftView.Web.Models;

public class SetupViewModel
{
    public bool    TenancyConfigured { get; init; }
    public string? TenancyName       { get; init; }
    public bool    ProjectCreated    { get; init; }
    public int     ProjectCount      { get; init; }
}

public class ConfirmEmailResultViewModel
{
    public bool    Success  { get; init; }
    public string  Message  { get; init; } = string.Empty;
    public string? Redirect { get; init; }
}

public class RegisterAuthorViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Your name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    [Display(Name = "Your name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Workspace name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Workspace name must be between 2 and 100 characters.")]
    [Display(Name = "Workspace name")]
    public string TenancyName { get; set; } = string.Empty;
}
