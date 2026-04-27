using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using PRES1.Models;

namespace PRES1.Controllers
{
    public class GoogleCaptchaService
    {
        private readonly IConfiguration _config;

        public GoogleCaptchaService(IConfiguration configuration)
        {
            this._config = configuration;
        }
        public string VerifyToken(string token)
        {
            try
            {
                //var configuration = new ConfigurationBuilder().Build();
                var secret = _config["RecaptchaSettings:GoogleRecaptchaSecretKey"];
                string url = "https://www.google.com/recaptcha/api/siteverify?secret=" + secret + "&response=" + token;

                using (var client = new WebClient())
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    var httpResult = client.DownloadString(url);
                    var googleResult = JsonConvert.DeserializeObject<GoogleCaptchaResponse>(httpResult);

                    if (googleResult.success & googleResult.score > 0.5)
                    {
                        return "true";
                    }
                    else
                    {
                        return "false";
                    }
                }
            }
            catch (Exception e)
            {
                return "false";
            }

        }
    }
}
