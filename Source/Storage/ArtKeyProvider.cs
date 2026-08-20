using Verse;

namespace Ustas.RimAI.Art.Storage
{
    public static class ArtKeyProvider
    {
        public static bool TryGetKey(Thing thing, out ArtKey key)
        {
            key = null;
            if (thing == null || thing.DestroyedOrNull()) return false;

            var loadId = thing.GetUniqueLoadID();
            if (string.IsNullOrEmpty(loadId)) return false;

            var mapId = thing.Map?.uniqueID ?? 0;
            key = new ArtKey($"{loadId}|{mapId}");
            return key.IsValid;
        }
    }
}
