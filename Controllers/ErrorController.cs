using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace PRES1.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error")]
        public IActionResult Index()
        {
            // Get the current culture
            var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var currentCulture = requestCulture?.RequestCulture.Culture;

            // Define the Welsh culture (you can adjust this if needed)
            var welshCulture = new CultureInfo("cy-GB");

            // Check if the current culture is Welsh
            var isWelsh = currentCulture != null && currentCulture.Equals(welshCulture);

            if (Response.StatusCode == StatusCodes.Status500InternalServerError)
            {
                return isWelsh ? Redirect("/cy/500-gwall") : Redirect("/500-error");
            }
            else if (Response.StatusCode != StatusCodes.Status200OK)
            {
                return isWelsh ? Redirect("/cy/404-gwall") : Redirect("/404-error");
            }
            return isWelsh ? Redirect("/cy") : Redirect("/");
        }
    }
}
