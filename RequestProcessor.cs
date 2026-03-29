#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
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

public interface IExternalApi
{
    Task<string> GetDescription(string request);
}

public class RequestProcessor : BackgroundService
{
    IExternalApi _externalApi;

    private readonly ILogger<RequestProcessor> _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestProcessor>();

    [Fact]
    public async Task RunProcess()
    {
        _logger.LogInformation("Запустились...");
        _externalApi = new ExternalApi();
        var requests = GetData();
        await ProcessNaive(requests);
    }

    public IEnumerable<Request> GetData()
    {
        List<Request> values = new();
        Enumerable.Range(0, 1000).ToList().ForEach(x => values.Add(new Request(x)));
        return values;
    }

    public async Task ProcessNaive(IEnumerable<Request> values)
    {
        foreach (var value in values)
        {
            Result process = await Process(value);
            process = await ProcessExternal(process);
            await SaveToDb(process);
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

    private async Task<Result> ProcessExternal(Result result)
    {
        Task<string> description;
        lock (_externalApi)
        {
            description = _externalApi.GetDescription(result.Value.ToString());
        }

        result.Message = description.Result;
        return result;
    }

    private async Task SaveToDb(Result value)
    {
        // Эмуляция сохранения записи в БД
        await Task.Delay(100);
        Console.WriteLine($"Result saved. Value: {value.Value}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RequestProcessor работает...");
            await Task.Delay(10000, stoppingToken);
        }
    }
}

public class ExternalApi : IExternalApi
{
    public async Task<string> GetDescription(string request)
    {
        await Task.Delay(100);
        return await Task.FromResult($"LLM Response: {request}");
    }
}