using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.art
{
    public sealed class ArtMeta
    {
        public Thing Thing { get; }
        public CompArt CompArt { get; }
        public CompProperties_Art ArtProps { get; }

        public string DefName { get; }
        public string ModName { get; }
        public string PackageId { get; }
        public string ThingLabel { get; }

        public string OriginalTitle { get; }
        public string AuthorName { get; }
        public string OriginalDescription { get; }
        public QualityCategory? Quality { get; }

        public ArtMeta(Thing thing)
        {
            Thing = thing ?? throw new System.ArgumentNullException(nameof(thing));
            CompArt = thing.TryGetComp<CompArt>();
            ArtProps = CompArt?.Props;

            var def = thing.def;
            DefName = def?.defName ?? "UnknownDef";
            ModName = def?.modContentPack?.Name ?? "Unknown";
            PackageId = def?.modContentPack?.PackageId ?? "Unknown";
            ThingLabel = thing.LabelCap ?? def?.label ?? "Unknown";

            if (thing.TryGetQuality(out var quality))
                Quality = quality;

            OriginalTitle = CompArt != null && CompArt.Active
                ? CompArt.Title
                : ThingLabel;
            AuthorName = CompArt?.AuthorName ?? string.Empty;

            if (CompArt != null && CompArt.Active)
            {
                var imageDescription = CompArt.GenerateImageDescription();
                OriginalDescription = imageDescription != null ? imageDescription.ToString() : string.Empty;
            }
            else
            {
                OriginalDescription = def?.description ?? string.Empty;
            }
        }
    }
}
