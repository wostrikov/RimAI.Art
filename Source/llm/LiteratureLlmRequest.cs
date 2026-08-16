namespace Ustas.RimAI.Art.llm
{
    /// <summary>
    /// Request data for Literature Expansion's standalone structured-generation pipeline.
    /// This intentionally does not inherit from or wrap Ustas.RimAI.Communication.Data.TalkRequest, because
    /// these requests are not pawn dialogue and must never enter RimTalk's talk queue.
    /// </summary>
    public sealed class LiteratureLlmRequest
    {
        public string Instruction { get; }
        public string Context { get; set; }

        public LiteratureLlmRequest(string instruction)
        {
            Instruction = instruction ?? string.Empty;
        }
    }
}
