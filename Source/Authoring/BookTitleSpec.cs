/*
 * Purpose:
 * - Structured LLM output for book title generation.
 *
 * Uses:
 * - JSON serialization/deserialization.
 *
 * Fields:
 * - title: localized book title
 * - synopsis: short book description
 *
 * Design notes:
 * - This is a pure DTO.
 *
 * Do NOT:
 * - Do not add behavior or logic.
 * - Do not reference RimWorld or RimTalk APIs.
 */
using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.Authoring
{
    [DataContract]
    public sealed class BookTitleSpec : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "synopsis")]
        public string Synopsis { get; set; }

        public string GetText()
        {
            if (string.IsNullOrWhiteSpace(Title)) return Synopsis ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Synopsis)) return Title ?? string.Empty;
            return $"{Title}: {Synopsis}";
        }
    }
}
