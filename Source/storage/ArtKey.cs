namespace Ustas.RimAI.Art.storage
{
    public sealed class ArtKey
    {
        public string Id { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Id);

        public ArtKey(string id)
        {
            Id = id ?? string.Empty;
        }

        public override string ToString()
        {
            return Id ?? string.Empty;
        }
    }
}
