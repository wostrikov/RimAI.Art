using System.Threading.Tasks;
using Ustas.RimAI.Art.LLM;
using Ustas.RimAI.Art.Synopsis.LLM;

namespace Ustas.RimAI.Art.TV
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
