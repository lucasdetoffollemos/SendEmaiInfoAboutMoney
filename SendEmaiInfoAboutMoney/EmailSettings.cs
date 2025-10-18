using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendEmaiInfoAboutMoney
{
    public class EmailSettings
    {
        public string SmtpFromEmail { get; set; } = string.Empty;
        public string SmtpToEmail { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
       
    }
}
