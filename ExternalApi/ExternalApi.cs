using Microsoft.Extensions.Logging;
using ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces;
using Sber.LP.TestProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessingAsyncWithExternalLlm.ExternalApi;

public class ExternalApi : IExternalApi
{
    private readonly int _apiId;
    private readonly ILogger<ExternalApi> _logger;
    private static readonly Random _random = new Random();

    public ExternalApi(ILogger<ExternalApi> logger, int apiId)
    {
        _logger = logger;
        _apiId = apiId;
    }

    public async Task<string> GetDescription(string value, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("API #{ApiId} обрабатывает значение {Value}", _apiId, value);

        // Имитация задержки
        await Task.Delay(100, cancellationToken);

        //// Имитация случайных сбоев (10% вероятность)
        //if (_random.Next(1, 100) <= 10)
        //{
        //    throw new Exception($"Внешний API #{_apiId} временно недоступен");
        //}

        return $"Описание для значения {value} от API #{_apiId}";
    }
}
