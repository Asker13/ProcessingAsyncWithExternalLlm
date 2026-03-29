using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessingAsyncWithExternalLlm;
using Sber.LP.TestProcessor;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddLogging();
            services.AddHostedService<RequestProcessor>();
            services.AddHostedService<RequestProcessorHostedService>();
        })
        .Build();

        // Запустите хост (например, для фоновых сервисов)
        await host.RunAsync();
    }
}