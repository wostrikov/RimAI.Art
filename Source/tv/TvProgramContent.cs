using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Art.tv
{
    [DataContract]
    public sealed class TvProgramContent : IJsonData
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "content")]
        public string Content { get; set; }

        public string GetText()
        {
            if (string.IsNullOrWhiteSpace(Title)) return Content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Content)) return Title ?? string.Empty;
            return $"{Title}: {Content}";
        }
    }
}
