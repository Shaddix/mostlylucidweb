using System.ComponentModel.DataAnnotations;

namespace Mostlylucid.AI.Models.ViewModels;

public class ContactViewModel : AIBaseViewModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name must be less than 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Company name must be less than 100 characters")]
    public string? Company { get; set; }

    [Required(ErrorMessage = "Please select a project type")]
    public string ProjectType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message is required")]
    [StringLength(5000, MinimumLength = 20, ErrorMessage = "Message must be between 20 and 5000 characters")]
    public string Message { get; set; } = string.Empty;
}
