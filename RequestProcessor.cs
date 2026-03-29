#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ProcessingAsyncWithExternalLlm.ExternalApi;
using ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces;
//using ProcessingAsyncWithExternalLlm.Progress;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

/*
 * Джун реализовал задачу по обработке и сохранению данных.
 *
 * RequestProcessor - получает данные, обрабатывает и сохраняет их в БД
 * Метод Process - обработка данных на стороне сервиса
 * Метод ProcessExternal - обработка данных с помощью внешнего API.
 * Метод SaveToDb - сохранение данных в БД.
 *
 * RunProcess - запускает процесс обработки данных.
 * Текущие проблемы:
 * - Метод RunProcess работает очень долго и пользователи жалуются,
 * - Внешний сервис периодически падает, так как там есть свои ограничения
 * - Тимлид недоволен тем, что код очень сырой проблемный и не примет его
 * Нужно:
 * - Провести ревью кода, указать на возможные проблемы
 * - Ускорить обработку и сохранение данных
 * - По возможности сделать код более поддерживаемым
 * - Дать дальнейшие рекомендации по улучшению, если они есть
 *
 * Требования к решению:
 * - Решение залить на github (или gist)
 * - Решение должно запускать в виде теста или любым другим способом
 * - При отсутствии времени сделать упор на ускорение
 * - При трудоемкости реализации описать идею словами
 * - По решению и идеям реализации могут быть заданы вопросы
 */

namespace Sber.LP.TestProcessor;

public record Request(int Value);

public class Result
{
    public int Value { get; set; }
    public string Message { get; set; }
}

public class RequestProcessor
{
    private ILogger<RequestProcessor> _logger;
    private readonly IExternalApiPool _externalApiPool;
    private readonly RequestProcessorOptions _options;
    private readonly IAsyncPolicy _retryPolicy;

    public RequestProcessor(IExternalApiPool externalApiPool, ILogger<RequestProcessor> logger, IOptions<RequestProcessorOptions> options)
    {
        _externalApiPool = externalApiPool ?? throw new ArgumentNullException(nameof(externalApiPool));
        _logger = logger;
        _options = options.Value;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                onRetry: (ex, time, retryCount, ctx) =>
                {
                    _logger.LogWarning(ex, "Повтор #{RetryCount} после {Delay}ms", retryCount, time.TotalMilliseconds);
                });
    }

    [Fact]
    public async Task RunProcess()
    {
        // Настройка зависимостей
        //var externalApiPool = _externalApiPool;
        //var processor = new RequestProcessor(externalApiPool, _logger, MaxConcurrency: 5, BatchSize: 50);
        var requests = GetData();

        //var progress = new Progress<ProgressInfo>(p =>
        //{
        //    Console.WriteLine($"[{p.Stage}] Прогресс: {p.Processed}/{p.Total} ({p.Percent:F1}%)");
        //});

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        await ProcessOptimized(requests, _options.MaxConcurrency, _options.BatchSize, cts.Token);
    }

    /// <summary>
    /// Разбивает список на батчи указанного размера.
    /// </summary>
    private List<List<T>> SplitIntoBatches<T>(List<T> source, int batchSize)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Размер батча должен быть положительным.");
        if (source == null || source.Count == 0) return new List<List<T>>();

        var batches = new List<List<T>>((source.Count + batchSize - 1) / batchSize);
        for (int i = 0; i < source.Count; i += batchSize)
        {
            var batch = source.GetRange(i, Math.Min(batchSize, source.Count - i));
            batches.Add(batch);
        }
        _logger.LogInformation("Сформировано {BatchCount} батчей, размер батча: {BatchSize}", batches.Count, batchSize);
        return batches;
    }

    public IEnumerable<Request> GetData()
    {
        List<Request> values = new();
        Enumerable.Range(0, 1000).ToList().ForEach(x => values.Add(new Request(x)));
        return values;
    }

    /// <summary>
    /// Оптимизированная обработка с поддержкой прогресса через IProgress
    /// </summary>
    public async Task ProcessOptimized(IEnumerable<Request> values, /*IProgress<ProgressInfo>? progress = null,*/
        int maxConcurrency, int batchSize, CancellationToken cancellationToken = default)
    {
        var requestList = values.ToList();//Защита от дурака
        _logger.LogInformation("Старт обработки для {Total} запросов", requestList.Count);

        //// Отправляем начальное состояние
        //progress?.Report(new ProgressInfo { Processed = 0, Total = requestList.Count, Stage = "Начало обработки" });

        // 1. Этап вызова внешнего API
        var externalResults = await ProcessExternalApiBatchAsync(requestList, /*progress,*/ maxConcurrency, batchSize, cancellationToken);

        if (externalResults.Count == 0)
        {
            _logger.LogWarning("Нет успешных результатов от внешнего API");
            //progress?.Report(new ProgressInfo { Processed = 0, Total = requestList.Count, Stage = "Ошибка: нет данных от API", IsCompleted = true });
            return;
        }

        //progress?.Report(new ProgressInfo { Processed = externalResults.Count, Total = requestList.Count, Stage = "Вызов API завершён" });

        // 2. Сохранение в БД пакетом
        await SaveToDatabaseAsync(externalResults, /*progress,*/ cancellationToken);

        //progress?.Report(new ProgressInfo { Processed = externalResults.Count, Total = requestList.Count, Stage = "Завершено успешно", IsCompleted = true });
        _logger.LogInformation("Обработка завершена. Успешно: {SuccessCount} из {Total}", externalResults.Count, requestList.Count);
    }

    /// <summary>
    /// Обработка через внешний API с контролем параллелизма
    /// </summary>
    private async Task<List<Result>> ProcessExternalApiBatchAsync(List<Request> requests, /*IProgress<ProgressInfo>? progress,*/
        int maxConcurrency, int batchSize, CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<Result>();
        var errors = new ConcurrentBag<Exception>();
        var processedCount = 0;

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var requestBatches = SplitIntoBatches(requests, batchSize);
        _logger.LogInformation("Разбито на {BatchCount} батчей по {BatchSize} запросов", requestBatches.Count, batchSize);

        var batchTasks = requestBatches.Select(async requestBatch =>
        {
            var batchResults = new List<Result>();

            foreach (var request in requestBatch)
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await Process(request, cancellationToken);
                    result = await ProcessExternalWithRetry(result, cancellationToken);
                    _logger.LogInformation("Request после Внешнего сервиса {Value} {Message}", result.Value, result.Message);
                    batchResults.Add(result);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Отмена обработки запроса {Value}", request.Value);
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogError(ex, "Ошибка при обработке запроса {Value}", request.Value);
                }
                finally
                {
                    semaphore.Release();
                    //var current = Interlocked.Increment(ref processedCount);
                    //progress?.Report(new ProgressInfo
                    //{
                    //    Processed = current,
                    //    Total = requests.Count,
                    //    Stage = $"Обработка API (батч {batchResults.Count}/{requestBatch.Count})"
                    //});
                }
            }

            foreach (var result in batchResults)
            {
                results.Add(result);
            }
        });

        await Task.WhenAll(batchTasks);

        if (errors.Any())
        {
            _logger.LogWarning("Всего ошибок при вызове API: {ErrorCount}", errors.Count);
        }

        return results.ToList();
    }

    private async Task<Result> Process(Request value, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return new Result { Value = value.Value * 2 };
    }

    private async Task<Result> ProcessExternalWithRetry(Result result, CancellationToken cancellationToken = default)
    {
        var externalApi = await _externalApiPool.TakeExternalApiAsync(cancellationToken);
        try
        {
            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                var description = await externalApi.GetDescription(result.Value.ToString(), ct);
                result.Message = description;
                return result;
            }, cancellationToken);
        }
        finally
        {
            _externalApiPool.Release(externalApi);
        }
    }
    

    private async Task<Result> Process(Request value)
    {
        await Task.Delay(100);

        return new Result()
        {
            Value = value.Value * 2
        };
    }

    /// <summary>
    /// Сохранение в БД Всей пачкой
    /// </summary>
    private async Task SaveToDatabaseAsync(List<Result> results, /*IProgress<ProgressInfo>? progress, */ CancellationToken cancellationToken)
    {
        _logger.LogInformation("Сохранение {Count} записей в БД...", results.Count);
        //progress?.Report(new ProgressInfo { Processed = 0, Total = results.Count, Stage = "Сохранение в БД" });
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Сохранено {Count} записей в БД.", results.Count);
    }


}
