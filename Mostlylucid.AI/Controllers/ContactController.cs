using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;
using Mostlylucid.Services.Email;
using Mostlylucid.Shared.Models.Email;

namespace Mostlylucid.AI.Controllers;

public class ContactController : AIBaseController
{
    private readonly IEmailService _emailService;

    public ContactController(
        AIBaseControllerService baseControllerService,
        IEmailService emailService,
        ILogger<ContactController> logger)
        : base(baseControllerService, logger)
    {
        _emailService = emailService;
    }

    public IActionResult Index()
    {
        var model = new ContactViewModel();
        PopulateAnalytics(model);

        if (Request.IsHtmx())
        {
            return PartialView(model);
        }

        ViewBag.Title = "Contact";
        ViewBag.Description = "Get in touch to discuss your AI project. Let's explore how practical AI solutions can help your business.";

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Please check your input and try again." });
        }

        try
        {
            // Build the email content
            var emailContent = $@"
New Contact from Mostlylucid.AI

Name: {model.Name}
Email: {model.Email}
Company: {model.Company ?? "Not provided"}
Project Type: {model.ProjectType}

Message:
{model.Message}
";

            // Send notification email
            await _emailService.SendContactEmail(new ContactEmailModel
            {
                SenderEmail = model.Email,
                SenderName = model.Name,
                Content = emailContent,
                Subject = $"New AI Consultation Request: {model.ProjectType}"
            });

            Logger.LogInformation("Contact form submitted from {Email} - {ProjectType}", model.Email, model.ProjectType);

            return Ok(new { message = "Thank you for your message! I'll get back to you soon." });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send contact form email from {Email}", model.Email);
            return StatusCode(500, new { message = "Sorry, there was an error sending your message. Please try again later." });
        }
    }
}
