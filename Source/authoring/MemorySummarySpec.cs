/*
 * Purpose:
 * - Structured LLM output for summarizing pawn memories.
 *
 * Uses:
 * - JSON serialization/deserialization.
 *
 * Fields (example):
 * - summary: natural language memory summary
 * - keywords / tone (optional)
 *
 * Design notes:
 * - Used as an intermediate artifact for book authoring.
 *
 * Do NOT:
 * - Do not store pawn IDs or world state.
 * - Do not include book-specific formatting here.
 */
using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.authoring
{
    [DataContract]
    public sealed class MemorySummarySpec : IJsonData
    {
        [DataMember(Name = "summary")]
        public string Summary { get; set; }

        [DataMember(Name = "keywords", EmitDefaultValue = false)]
        public string[] Keywords { get; set; }

        [DataMember(Name = "tone", EmitDefaultValue = false)]
        public string Tone { get; set; }

        public string GetText()
        {
            return Summary ?? string.Empty;
        }
    }
}
