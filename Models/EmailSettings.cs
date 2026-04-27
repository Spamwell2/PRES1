using System.Net.Mail;

namespace PRES1.Models
{
    public class EmailSettings
    {
        public string FallbackEmail { get; set; }
        public string ErrorNotificationEmail { get; set; }
        public SmtpSettings Smtp { get; set; }

        public class SmtpSettings
        {
            public string From { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public SmtpDeliveryMethod DeliveryMethod { get; set; }
            public string PickupDirectoryLocation { get; set; }
            public bool EnableSsl { get; set; }
        }
    }
}
