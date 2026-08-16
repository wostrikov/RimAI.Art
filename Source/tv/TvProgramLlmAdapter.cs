using System.Threading.Tasks;
using Ustas.RimAI.Art.llm;
using Ustas.RimAI.Art.synopsis.llm;

namespace Ustas.RimAI.Art.tv
{
    public static class TvProgramLlmAdapter
    {
        public static Task<TvProgramContent> QueryAsync(LiteratureLlmRequest request)
        {
            if (request == null) return Task.FromResult<TvProgramContent>(null);
            return IndependentBookLlmClient.QueryJsonAsync<TvProgramContent>(request);
        }
    }
}
