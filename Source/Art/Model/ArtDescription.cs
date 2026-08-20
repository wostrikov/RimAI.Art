using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.Art.Model
{
    [DataContract]
    public sealed class ArtDescription : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }

        public string GetText()
        {
            if (string.IsNullOrWhiteSpace(Title)) return Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Text)) return Title ?? string.Empty;
            return $"{Title}: {Text}";
        }
    }
}
