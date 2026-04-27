using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Mail;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Infrastructure.PublishedCache;

public class CommentsController : SurfaceController
{
    private readonly IContentService _contentService;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly IConfiguration _configuration;

    public CommentsController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IContentService contentService,
        IUmbracoDatabaseFactory databaseFactory,
        ServiceContext services,
        AppCaches appCaches,
        IProfilingLogger profilingLogger,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedContentQuery publishedContentQuery,
        IConfiguration configuration)
        : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
    {
        _contentService = contentService;
        _publishedContentQuery = publishedContentQuery;
        _configuration = configuration;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult SubmitComment(Guid postId, string author, string email, string content)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Name and comment content cannot be empty." });
            }

            // Retrieve the parent Blog Post node
            var parentNode = _contentService.GetById(postId);
            if (parentNode == null)
            {
                return Json(new { success = false, message = "Blog post not found." });
            }

            // Create a new Comment node
            var commentName = $"Comment - {author}";
            var commentNode = _contentService.Create(commentName, parentNode, "comment");
            commentNode.SetValue("author", author);
            commentNode.SetValue("email", email);
            commentNode.SetValue("content", content);
            commentNode.SetValue("datePosted", DateTime.Now);
            commentNode.SetValue("approved", false);

            // Save and publish the comment
            _contentService.SaveAndPublish(commentNode);

            // Send email notification with backoffice link
            var parentNodePublished = _publishedContentQuery.Content(postId);
            var notificationEmail = parentNodePublished?.Value<string>("notificationEmail");
            var fallbackEmail = _configuration["EmailSettings:FallbackEmail"];
            var recipientEmail = !string.IsNullOrEmpty(notificationEmail) ? notificationEmail : fallbackEmail;

            var backOfficeLink = GenerateBackOfficeLink(commentNode.Key);
            var backOfficePath = GetBackOfficePath(commentNode);
            if (!string.IsNullOrEmpty(recipientEmail))
            {
                SendEmailNotification(recipientEmail, author, content, parentNodePublished?.Name ?? "Blog Post", backOfficeLink, backOfficePath);
            }

            // Success response
            return Json(new { success = true, message = "Your comment has been submitted and is awaiting approval." });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while submitting your comment. Please try again." });
        }
    }

    private string GetBackOfficePath(IContent content)
    {
        var pathSegments = new List<string>();
        var current = content;

        while (current != null && current.Level > 1) // Skip root (-1) and Recycle Bin
        {
            pathSegments.Insert(0, current.Name);
            current = _contentService.GetParent(current);
        }

        return string.Join(" -> ", pathSegments);
    }

    private string GenerateBackOfficeLink(Guid commentKey)
    {
        // Generate the backoffice URL for the comment
        var backOfficeBaseUrl = _configuration["Umbraco:BackOfficeUrl"];
        return $"{backOfficeBaseUrl}/#/content/content/edit/{commentKey}";
    }

    private void SendEmailNotification(string toEmail, string commenterName, string commentContent, string postTitle, string backOfficeLink, string backOfficePath)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("EmailSettings:Smtp");
            var smtpHost = smtpSettings["Host"];
            var smtpPort = int.Parse(smtpSettings["Port"] ?? "25");
            var smtpUsername = smtpSettings["Username"];
            var smtpPassword = smtpSettings["Password"];
            var senderEmail = smtpSettings["From"];

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = false
            };

            var subject = $"New Comment on: {postTitle}";
            var body = $"A new comment has been submitted on your post '<strong>{postTitle}</strong>':<br><br>" +
                       $"<strong>Author:</strong> {commenterName}<br>" +
                       $"<strong>Comment:</strong> {commentContent}<br><br>" +
                       $"You can view and manage the comment directly in the back office by clicking the link below:<br>" +
                       $"<a href=\"{backOfficeLink}\">{backOfficeLink}</a><br><br>" +
                       $"If you are not logged in, you may need to log in first and then revisit this link, or manually navigate to:<br>" +
                       $"<em>{backOfficePath}</em>";


            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Corporate Blog"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            smtpClient.Send(mailMessage);
        }
        catch (Exception ex)
        {
            SendErrorEmail(ex);
        }
    }

    private void SendErrorEmail(Exception ex)
    {
        try
        {
            var smtpSettings = _configuration.GetSection("EmailSettings:Smtp");
            var fallbackEmail = _configuration["EmailSettings:ErrorNotificationEmail"];

            using var smtpClient = new SmtpClient(smtpSettings["Host"], int.Parse(smtpSettings["Port"] ?? "25"))
            {
                Credentials = new System.Net.NetworkCredential(smtpSettings["Username"], smtpSettings["Password"]),
                EnableSsl = false
            };

            var errorSubject = "Corporate Blog Comment Notification Error";
            var errorBody = $"An error occurred while sending a comment notification email:\n\n" +
                            $"Error Message: {ex.Message}\n" +
                            $"Inner Exception: {ex.InnerException?.Message ?? "None"}";

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings["From"], "NOREPLY"),
                Subject = errorSubject,
                Body = errorBody
            };

            mailMessage.To.Add(fallbackEmail);
            smtpClient.Send(mailMessage);
        }
        catch (Exception secondaryEx)
        {
            Console.WriteLine($"Failed to send error notification email: {secondaryEx.Message}");
        }
    }
}
