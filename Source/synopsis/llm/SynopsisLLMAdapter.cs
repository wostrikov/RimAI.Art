using System.Threading.Tasks;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.synopsis.model;

namespace Ustas.RimAI.Art.synopsis.llm
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
