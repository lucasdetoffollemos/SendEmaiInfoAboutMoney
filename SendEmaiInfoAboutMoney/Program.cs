
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace SendEmaiInfoAboutMoney
{
    public class Program
    {
        static async Task Main(string[] args)
        {

            IHost host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();

                    DotNetEnv.Env.Load();

                    // Register settings using Environment variables
                    services.Configure<EmailSettings>(options =>
                    {
                        options.SmtpFromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")!;
                        options.SmtpToEmail = Environment.GetEnvironmentVariable("SMTP_TO_EMAIL")!;
                        options.SmtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD")!;
                        options.ApiKey = Environment.GetEnvironmentVariable("API_KEY")!;
                    });
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

    }
}
