using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly EmailSettings _settings;

        public static decimal oldCurrency = 0;
        public static decimal newCurrency = 0;

        public Worker(ILogger<Worker> logger, IOptions<EmailSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Email from {emailFrom}", _settings.SmtpFromEmail);
                _logger.LogInformation("Im passing here NOW MANN. Hour: {hour}", DateTime.Now);
                await CheckWhichNumberIsShorter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending email, {ErrorMessage}", ex.Message);
            }
        }

        private async Task CheckWhichNumberIsShorter()
        {
            //caso seja a primeira vez apenas pegar o valor do dia da moeda
            decimal actualCurrency = 0;

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
                _logger.LogInformation("New currency is greater or equal than the old currency, just updating value. Old value {oldValue}, new value {newValue}", oldCurrency, actualCurrency);
                oldCurrency = actualCurrency;
            }

        }

        private async Task<decimal> GetCurrencyRateFromEURToBRLAsync()
        {
            try
            {
                using var httpClient = new HttpClient();

                var url = $"https://api.currencyapi.com/v3/latest?apikey={_settings.ApiKey}&currencies=BRL&base_currency=EUR";

                var response = await httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                var rate = doc.RootElement.GetProperty("data").GetProperty("BRL").GetProperty("value").GetDecimal();

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
            var fromAddress = new MailAddress(_settings.SmtpFromEmail, "Cotação Euro");
            var toAddress = new MailAddress(_settings.SmtpToEmail);
            var fromPassword = _settings.SmtpPassword;

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
