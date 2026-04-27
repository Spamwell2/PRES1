using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Mail;
using System.Text;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Extensions;
using PRES1.Models;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.PublishedModels;

namespace PRES1.Controllers
{
    public class ContactFormController : SurfaceController
    {
        private readonly IConfiguration _configuration;
        private readonly EmailSettings _emailSettings;

        public ContactFormController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedUrlProvider publishedUrlProvider,
            IConfiguration configuration)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
        {
            _configuration = configuration;
            _emailSettings = _configuration.GetSection("EmailSettings").Get<EmailSettings>();
        }

        [HttpPost]
        public IActionResult Submit(ContactFormViewModel model)
        {
            bool success = false;
            if (!ModelState.IsValid)
            {
                return Json(success);
            }
            success = SendEmail(model);
            return Json(success);
        }

        private bool SendEmail(ContactFormViewModel model)
        {
            var smtpSettings = _emailSettings.Smtp;
            var contactPage = UmbracoContext.Content.GetById(CurrentPage.Id);

            if (smtpSettings == null || string.IsNullOrEmpty(_emailSettings.FallbackEmail))
            {
                return false;
            }

            using var client = new SmtpClient
            {
                Host = smtpSettings.Host,
                Port = smtpSettings.Port,
                EnableSsl = smtpSettings.EnableSsl,
                Credentials = new System.Net.NetworkCredential(smtpSettings.Username, smtpSettings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(smtpSettings.From, "Eventify"),
                Subject = $"Message from Eventify - {model.Subject}",
                Body = BuildEmailBody(model),
                IsBodyHtml = true
            };
            message.To.Add(model.Email);

            if (contactPage.HasValue("toEmail"))
            {
                var bccEmail = contactPage.Value<string>("toEmail");
                var bccs = bccEmail.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var bcc in bccs)
                {
                    message.Bcc.Add(bcc);
                }
            }
            else
            {
                message.Bcc.Add(_emailSettings.FallbackEmail);
            }

            client.Send(message);
            return true;
        }

        private string BuildEmailBody(ContactFormViewModel model)
        {
            var comments = model.Message.Replace(Environment.NewLine, "<br />");

            var emailBody = new StringBuilder();
            emailBody.Append("<table border='1' cellpadding='10' cellspacing='0' width='600'>");
            emailBody.AppendFormat("<tr><td valign='top' bgcolor='#cccccc'><strong>Name:</strong></td><td>{0}</td></tr>", model.Name);
            emailBody.AppendFormat("<tr><td valign='top' bgcolor='#cccccc'><strong>Email:</strong></td><td>{0}</td></tr>", model.Email);
            emailBody.AppendFormat("<tr><td valign='top' bgcolor='#cccccc'><strong>Message:</strong></td><td>{0}</td></tr>", comments);
            emailBody.Append("</table>");

            return emailBody.ToString();
        }
        private void SendErrorEmail(Exception ex)
        {
            var smtpSettings = _emailSettings.Smtp;

            var client = new SmtpClient
            {
                Host = smtpSettings?.Host ?? "localhost",
                Port = smtpSettings?.Port ?? 25,
                EnableSsl = smtpSettings?.EnableSsl ?? false,
                Credentials = new System.Net.NetworkCredential(
                    smtpSettings?.Username ?? string.Empty,
                    smtpSettings?.Password ?? string.Empty)
            };

            var errorEmail = new MailMessage
            {
                Subject = "Eventify Contact Form Error",
                Body = $"Error: {ex.Message}<br>InnerException: {ex.InnerException?.Message}",
                IsBodyHtml = true,
                From = new MailAddress(smtpSettings?.From ?? "noreply@merthyr.gov.uk", "MTCBC")
            };

            errorEmail.To.Add(_emailSettings.ErrorNotificationEmail);
            client.Send(errorEmail);
        }
    }
}



