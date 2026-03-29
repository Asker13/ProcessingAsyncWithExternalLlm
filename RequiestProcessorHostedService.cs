using Microsoft.Extensions.Hosting;
using Sber.LP.TestProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessingAsyncWithExternalLlm;

public class RequestProcessorHostedService : IHostedService
{
    private readonly RequestProcessor _processor;
    private Task _executingTask;
    private CancellationTokenSource _cts;

    public RequestProcessorHostedService(RequestProcessor processor)
    {
        _processor = processor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executingTask = Task.Run(() => _processor.RunProcess(), _cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        await _executingTask;
    }
}