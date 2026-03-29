using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces;
using Sber.LP.TestProcessor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessingAsyncWithExternalLlm.ExternalApi
{
    /// <summary>
    /// Пул экземпляров внешнего API с ограничением количества
    /// </summary>
    public class ExternalApiPool : IExternalApiPool
    {
        private readonly ConcurrentBag<ExternalApi> _availableApis;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<ExternalApiPool> _logger;
        ExternalApiPoolOptions _options;

        private int _inUseCount;

        public ExternalApiPool(ILogger<ExternalApiPool> logger, ILogger<ExternalApi> logExternalApi, IOptions<ExternalApiPoolOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _availableApis = new ConcurrentBag<ExternalApi>();
            _semaphore = new SemaphoreSlim(_options.MaxPoolSize, _options.MaxPoolSize);

            // Инициализируем пул
            for (int i = 0; i < _options.MaxPoolSize; i++)
            {
                var api = new ExternalApi(logExternalApi, i + 1);
                _availableApis.Add(api);
            }

            _logger.LogInformation("Создан пул ExternalApi с {MaxSize} экземплярами", _options.MaxPoolSize);
        }

        public int AvailableCount => _availableApis.Count;
        public int InUseCount => _inUseCount;

        public async Task<IExternalApi> TakeExternalApiAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            if (_availableApis.TryTake(out var api))
            {
                Interlocked.Increment(ref _inUseCount);
                _logger.LogInformation("Взят API из пула. Доступно: {Available}, Используется: {InUse}",
                    AvailableCount, InUseCount);
                return api;
            }

            // Этого не должно произойти из-за семафора, но на всякий случай
            throw new InvalidOperationException("Нет доступных экземпляров API");
        }

        public void Release(IExternalApi api)
        {
            if (api is ExternalApi externalApi)
            {
                _availableApis.Add(externalApi);
                Interlocked.Decrement(ref _inUseCount);
                _semaphore.Release();
                _logger.LogInformation("Возвращён API в пул. Доступно: {Available}, Используется: {InUse}",
                    AvailableCount, InUseCount);
            }
            else
            {
                _logger.LogWarning("Попытка вернуть в пул объект неверного типа");
            }
        }
    }
}
