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
        private readonly IHostApplicationLifetime _lifetime;

        public Worker(ILogger<Worker> logger, IOptions<EmailSettings> settings, IHostApplicationLifetime lifetime)
        {
            _logger = logger;
            _settings = settings.Value;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var dateTimeNow = DateTime.Now;

            try
            {
                _logger.LogInformation("Im passing here NOW MANN. Hour: {hour}", dateTimeNow);
                
                await CheckWhichNumberIsShorter(dateTimeNow);

                //await GetCurrencyRateYesterdayFromEURToBRLAsync(dateTimeNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending email, {ErrorMessage}", ex.Message);
            }

            //parando aplicação
            _logger.LogInformation("Stopping aplication");
            _lifetime.StopApplication();
        }

        private async Task CheckWhichNumberIsShorter(DateTime dateTime)
        {
            decimal todayCurrency = 0;

            try
            {
                todayCurrency = await GetCurrencyRateFromEURToBRLAsync();
                todayCurrency = Math.Round(todayCurrency, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting today currency async. {ErrorMessage}", ex.Message);
                throw;
            }

            decimal yesterdayCurrency = 0;

            try
            {
                yesterdayCurrency = await GetCurrencyRateYesterdayFromEURToBRLAsync(dateTime);
                yesterdayCurrency = Math.Round(yesterdayCurrency, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting yesterday currency async. {ErrorMessage}", ex.Message);
                throw;
            }

            //caso o valor antigo seja maior que o valor novo
            //mandar email
            if (yesterdayCurrency > todayCurrency)
            {
                _logger.LogInformation("New currency is less than the old currency, sending email...");

                await SendEmailAsync(todayCurrency, yesterdayCurrency, dateTime);

                return;
            }
            
            _logger.LogInformation("Today currency is greater or equal than the old currency, just updating value. Yesterady value {oldValue}, Today value {newValue}", yesterdayCurrency, todayCurrency);
        }

        private async Task<decimal> GetCurrencyRateFromEURToBRLAsync()
        {
            try
            {
                using var httpClient = new HttpClient();

                var urlFromCurrenciToday = $"https://api.currencyapi.com/v3/latest?apikey={_settings.ApiKey}&currencies=BRL&base_currency=EUR";

                var responseFromCurrenciToday = await httpClient.GetStringAsync(urlFromCurrenciToday);

                using var doc = JsonDocument.Parse(responseFromCurrenciToday);
                var rate = doc.RootElement.GetProperty("data").GetProperty("BRL").GetProperty("value").GetDecimal();

                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trybg get actual currency. {ErrorMessage}", ex.Message);

                throw;
            }

        }

        private async Task<decimal> GetCurrencyRateYesterdayFromEURToBRLAsync(DateTime date)
        {
            try
            {

                var yesterday = date.AddDays(-1);

                var yesterdayFormatted = yesterday.ToString("yyyy-MM-dd");

                using var httpClient = new HttpClient();

                var urlFromCurrenciYesterday = $"https://api.currencyapi.com/v3/historical?date={yesterdayFormatted}&apikey={_settings.ApiKey}&currencies=BRL&base_currency=EUR";

                var responseFromCurrenciYesterday = await httpClient.GetStringAsync(urlFromCurrenciYesterday);

                using var doc = JsonDocument.Parse(responseFromCurrenciYesterday);
                var rate = doc.RootElement.GetProperty("data").GetProperty("BRL").GetProperty("value").GetDecimal();

                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trybg get actual currency. {ErrorMessage}", ex.Message);

                throw;
            }
        }

        private async Task SendEmailAsync(decimal todayCurrency, decimal yesterdayCurrency, DateTime dateTime)
        {
            var fromAddress = new MailAddress(_settings.SmtpFromEmail, "Cotação Euro");
            var toAddress = new MailAddress(_settings.SmtpToEmail);
            var fromPassword = _settings.SmtpPassword;

            var subject = "COTAÇÃO EURO BAIXOU HOJE GITHUB";

            var body = $@"
            <html>
              <body style='font-family: Arial, sans-serif; color: #333;'>
                <h2 style='color: #0078D7;'>💶 Cotação do Euro</h2>
                <p>O Euro hoje está mais barato, custando <strong style='color: green;'>R$ {todayCurrency}</strong>.</p>
                <p>Ontem estava <strong style='color: red;'>R$ {yesterdayCurrency}</strong>.</p>
                <hr />
                <p style='font-size: 12px; color: #888;'>Atualizado em {dateTime:dd/MM/yyyy HH:mm}</p>
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
