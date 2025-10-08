using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SendEmaiInfoAboutMoney
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public static decimal oldCurrency = 0;
        public static decimal newCurrency = 0;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // calcula proxima 4:00 AM
                var now = DateTime.UtcNow; ;
                var nextRun = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);

                if (now > nextRun)
                    nextRun = nextRun.AddDays(1); // agenda pro proximo dia se ja passoi das 8 am

                var delay = nextRun - now;

                _logger.LogInformation("Next run scheduled at: {time}", nextRun);


                try
                {
                    await CheckWhichNumberIsShorter();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro while sending email, {ErrorMessage}", ex.Message);
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task CheckWhichNumberIsShorter()
        {
            //caso seja a primeira vez apenas pegar o valor do dia da moeda
            decimal actualCurrency = 0;

            await SendEmailAsync();
            try
            {
                actualCurrency = await GetCurrencyRateFromEURToBRLAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting currency async. {ErrorMessage}", ex.Message);
                throw;
            }


            if (oldCurrency == 0)
            {
                _logger.LogInformation("The old currency is 0.");
                oldCurrency = actualCurrency;
                return;
            }

            newCurrency = actualCurrency;

            //caso o valor antigo seja maior que o valor novo
            //mandar email
            //setar o valor novo no antigo
            if (oldCurrency > newCurrency)
            {
                _logger.LogInformation("New currency is less than the old currency, sending email...");
                await SendEmailAsync();
                oldCurrency = newCurrency;
            }
            else
            {
                //fazer pegar o valor do dia e armazenar
                _logger.LogInformation("New currency is greater or equal than the old currency, just updating values");
                oldCurrency = actualCurrency;
            }

        }

        private async Task<decimal> GetCurrencyRateFromEURToBRLAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                var url = "https://api.fastforex.io/fetch-one?from=EUR&to=BRL";
                httpClient.DefaultRequestHeaders.Add("X-API-Key", "7ca41296d4-5ebad53b23-t3m8cd");
                var response = await httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                var rate = doc.RootElement.GetProperty("result").GetProperty("BRL").GetDecimal();

                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trybg get actual currency. {ErrorMessage}", ex.Message);

                throw;
            }

        }

        private async Task SendEmailAsync()
        {
            DotNetEnv.Env.Load();

            var fromAddressEnv = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
            var toAddressEnv = Environment.GetEnvironmentVariable("SMTP_TO_EMAIL");
            var fromPasswordEnv = Environment.GetEnvironmentVariable("SMTP_PASSWORD");


            if(string.IsNullOrEmpty(fromAddressEnv)|| string.IsNullOrEmpty(toAddressEnv) || string.IsNullOrEmpty(fromPasswordEnv))
            {
                throw new InvalidOperationException("Values from env cannot be null");
            }

            var fromAddress = new MailAddress(fromAddressEnv, "Cotação Euro");
            var toAddress = new MailAddress(toAddressEnv);
            var fromPassword = fromPasswordEnv;

            var subject = "COTAÇÃO EURO BAIXOU HOJE";

            var body = $@"
            <html>
              <body style='font-family: Arial, sans-serif; color: #333;'>
                <h2 style='color: #0078D7;'>💶 Cotação do Euro</h2>
                <p>O Euro hoje está mais barato, custando <strong style='color: green;'>R$ {newCurrency:F2}</strong>.</p>
                <p>Ontem estava <strong style='color: red;'>R$ {oldCurrency:F2}</strong>.</p>
                <hr />
                <p style='font-size: 12px; color: #888;'>Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}</p>
              </body>
            </html>";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            try
            {
                _logger.LogInformation("Sending mail message to {toAddress}", toAddress);

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    IsBodyHtml = true,
                    Subject = subject,
                    Body = body
                })
                {
                    await smtp.SendMailAsync(message, CancellationToken.None);
                }

                _logger.LogInformation("Mail sent sucessfully to address: {toAddress}", toAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending email async. {ErrorMessage}", ex.Message);

                throw;
            }

        }
    }
}
