using Ustas.RimAI.Art.Settings;
using Verse;

namespace Ustas.RimAI.Art.Art
{
    public static class ArtClassifier
    {
        public static ArtMeta Classify(Thing thing)
        {
            if (thing == null) return null;

            var settings = LiteratureMod.Settings;
            bool allowLabelEdits = settings != null && settings.allowArtLabelEdits;
            if (!ArtEditPolicy.ShouldGenerate(thing, allowLabelEdits))
                return null;

            return new ArtMeta(thing);
        }
    }
}
