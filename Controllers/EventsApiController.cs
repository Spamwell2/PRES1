using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PRES1.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace PRES1.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsApiController : UmbracoApiController
    {
        private const int EventsParentId = 1192;

        // Use -1 to save uploaded images at the root of Media.
        // Or set to a specific media folder ID if you want to organize uploaded images under a certain folder.
        private const int EventImagesMediaParentId = 1170;  // Events Media Folder

        private const long MaxImageBytes = 5 * 1024 * 1024; // 5MB

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly IContentService _contentService;
        private readonly IMediaService _mediaService;
        private readonly MediaFileManager _mediaFileManager;
        private readonly MediaUrlGeneratorCollection _mediaUrlGeneratorCollection;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

        public EventsApiController(
            IContentService contentService,
            IMediaService mediaService,
            MediaFileManager mediaFileManager,
            MediaUrlGeneratorCollection mediaUrlGeneratorCollection,
            IShortStringHelper shortStringHelper,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider)
        {
            _contentService = contentService;
            _mediaService = mediaService;
            _mediaFileManager = mediaFileManager;
            _mediaUrlGeneratorCollection = mediaUrlGeneratorCollection;
            _shortStringHelper = shortStringHelper;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10_000_000)]
        public IActionResult Create([FromForm] AddEventViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please correct the highlighted fields and try again."
                });
            }

            var allowedCategories = new[]
            {
                "Music",
                "Theatre",
                "Community",
                "Sports",
                "Christmas",
                "Food & Drink",
                "Education",
                "Walks, Tours & Outdoor Activities"
            };

            if (!allowedCategories.Contains(model.Category))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid category selected."
                });
            }

            var imageValidationError = ValidateMainImage(model.MainImage);
            if (imageValidationError != null)
            {
                return imageValidationError;
            }

            var eventsParent = _contentService.GetById(EventsParentId);
            if (eventsParent == null)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Events parent page could not be found."
                });
            }

            try
            {
                var cleanTitle = model.Title.Trim();
                var newEvent = _contentService.Create(cleanTitle, EventsParentId, "eventItemPage");

                newEvent.SetValue("pageTitle", cleanTitle);
                newEvent.SetValue("pageSubtitle", string.IsNullOrWhiteSpace(model.Subtitle) ? null : model.Subtitle.Trim());
                newEvent.SetValue("summaryText", model.SummaryText?.Trim());
                newEvent.SetValue("eventDate", model.EventDate);
                newEvent.SetValue("eventOrganiser", model.EventOrganiser?.Trim());
                newEvent.SetValue("organiserEmail", model.OrganiserEmail?.Trim());

                // Store category as a JSON array to align with Umbraco's flexible dropdown handling.
                newEvent.SetValue("category", JsonConvert.SerializeObject(new[] { model.Category }));

                if (!string.IsNullOrWhiteSpace(model.Tags))
                {
                    var tagValues = model.Tags
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToArray();

                    newEvent.SetValue("tags", JsonConvert.SerializeObject(tagValues));
                }
                else
                {
                    newEvent.SetValue("tags", JsonConvert.SerializeObject(Array.Empty<string>()));
                }

                if (model.MainImage != null && model.MainImage.Length > 0)
                {
                    var imageUdi = CreateImageMediaItem(model.MainImage, EventImagesMediaParentId);

                    // mainImage should be the alias of the Image Media Picker property.
                    // Umbraco's Media Picker accepts the selected media UDI as the saved value.
                    newEvent.SetValue("mainImage", imageUdi);
                }

                _contentService.SaveAndPublish(newEvent);

                return Ok(new
                {
                    success = true,
                    message = "Your event has been submitted successfully and is awaiting review.",
                    eventId = newEvent.Id,
                    eventName = newEvent.Name
                });
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred while creating the event."
                });
            }
        }

        private IActionResult? ValidateMainImage(IFormFile? image)
        {
            if (image == null || image.Length == 0)
            {
                return null;
            }

            if (image.Length > MaxImageBytes)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "The selected image is too large. Please upload an image smaller than 5MB."
                });
            }

            var extension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid image file type. Please upload a JPG, PNG or WEBP image."
                });
            }

            if (string.IsNullOrWhiteSpace(image.ContentType) || !AllowedImageContentTypes.Contains(image.ContentType))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid image content type. Please upload a JPG, PNG or WEBP image."
                });
            }

            return null;
        }

        private string CreateImageMediaItem(IFormFile file, int parentId)
        {
            var safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new InvalidDataException("The uploaded image filename is invalid.");
            }

            var mediaName = Path.GetFileNameWithoutExtension(safeFileName);
            if (string.IsNullOrWhiteSpace(mediaName))
            {
                mediaName = "Event image";
            }

            IMedia media = _mediaService.CreateMedia(mediaName, parentId, Constants.Conventions.MediaTypes.Image);

            using var stream = file.OpenReadStream();
            media.SetValue(
                _mediaFileManager,
                _mediaUrlGeneratorCollection,
                _shortStringHelper,
                _contentTypeBaseServiceProvider,
                Constants.Conventions.Media.File,
                safeFileName,
                stream);

            var saveResult = _mediaService.Save(media);
            if (!saveResult.Success)
            {
                throw new InvalidOperationException("The uploaded image could not be saved to the Umbraco media library.");
            }

            return Udi.Create(Constants.UdiEntityType.Media, media.Key).ToString();
        }
    }
}
