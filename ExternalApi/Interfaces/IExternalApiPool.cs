using Sber.LP.TestProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces;

public interface IExternalApiPool
{
    Task<IExternalApi> TakeExternalApiAsync(CancellationToken cancellationToken);
    void Release(IExternalApi api);
    int AvailableCount { get; }
    int InUseCount { get; }
}
