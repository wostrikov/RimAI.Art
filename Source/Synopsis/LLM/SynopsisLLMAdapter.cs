using System.Threading.Tasks;
using Ustas.RimAI.Art.LLM;
using Ustas.RimAI.Art.Synopsis.Model;

namespace Ustas.RimAI.Art.Synopsis.LLM
{
    public static class SynopsisLLMAdapter
    {
        public static Task<BookSynopsis> QuerySynopsisAsync(LiteratureLlmRequest request)
        {
            if (request == null) return Task.FromResult<BookSynopsis>(null);
            return IndependentBookLlmClient.QueryJsonAsync<BookSynopsis>(request);
        }
    }
}
