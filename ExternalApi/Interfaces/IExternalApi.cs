using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessingAsyncWithExternalLlm.ExternalApi.Interfaces
{
    public interface IExternalApi
    {
        Task<string> GetDescription(string request, CancellationToken token);
    }

}
