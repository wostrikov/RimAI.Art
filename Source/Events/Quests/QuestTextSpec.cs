/*
 * Purpose:
 * - JSON payload for LLM-generated quest text.
 *
 * Fields:
 * - title: quest title shown in UI.
 * - description: quest description shown in UI/letter.
 */
using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.Events.Quests
{
    [DataContract]
    public sealed class QuestTextSpec : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        public string GetText()
        {
            return Description ?? Title ?? string.Empty;
        }
    }
}
