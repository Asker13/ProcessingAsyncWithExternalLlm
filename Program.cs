using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessingAsyncWithExternalLlm;
using ProcessingAsyncWithExternalLlm.ExternalApi;
using ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces;
using Sber.LP.TestProcessor;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.AddConsole();
builder.Logging.AddDebug();


// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Регистрация сервисов приложения
builder.Services.AddSingleton<IExternalApiPool, ExternalApiPool>(sp =>
{
    var logger = sp.GetRequiredService < ILogger<ExternalApiPool>>();
    var logExternalApi = sp.GetRequiredService<ILogger<ExternalApi>>();
    var options = new ExternalApiPoolOptions
    {
        MaxPoolSize = 3
    };
    var optionsWrapper = Options.Create(options);

    return new ExternalApiPool(logger, logExternalApi, optionsWrapper);
});
builder.Services.AddSingleton<RequestProcessor>(sp =>
{
    var options = new RequestProcessorOptions
    {
        MaxConcurrency = 7,
        BatchSize = 80
    };
    var externalApiPool = sp.GetRequiredService<IExternalApiPool>();
    var logger = sp.GetRequiredService<ILogger<RequestProcessor>>();
    var optionsWrapper = Options.Create(options);

    return new RequestProcessor(externalApiPool, logger, optionsWrapper);
});

//// Добавление сервисов
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Получаем сервис вручную
using (var scope = app.Services.CreateScope())
{
    var myService = scope.ServiceProvider.GetRequiredService<RequestProcessor>();
    await myService.RunProcess();
}

// Настройка pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run(); 
