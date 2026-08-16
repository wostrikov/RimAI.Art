using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.events.letters
{
    [DataContract]
    public sealed class AllyDiplomacyLetterSpec : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }

        public string GetText()
        {
            return $"{Title}\n{Body}";
        }
    }

    [DataContract]
    public sealed class FamilyLetterSpec : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }

        [DataMember(Name = "giftKind")]
        public string GiftKind { get; set; }

        [DataMember(Name = "giftNote")]
        public string GiftNote { get; set; }

        public string GetText()
        {
            return $"{Title}\n{Body}\n{GiftNote}";
        }
    }
}
