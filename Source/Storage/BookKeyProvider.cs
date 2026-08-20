using Verse;

namespace Ustas.RimAI.Art.Storage
{
    public static class BookKeyProvider
    {
        public static bool TryGetKey(Thing thing, out BookKey key)
        {
            return TryGetKey(thing, null, out key);
        }

        public static bool TryGetKey(Thing thing, Map mapOverride, out BookKey key)
        {
            key = null;
            if (thing == null || thing.DestroyedOrNull()) return false;

            var loadId = thing.GetUniqueLoadID();
            if (string.IsNullOrEmpty(loadId)) return false;

            var mapId = mapOverride?.uniqueID ?? thing.Map?.uniqueID ?? 0;
            key = new BookKey($"{loadId}|{mapId}");
            return key.IsValid;
        }
    }
}
