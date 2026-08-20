using Verse;

namespace Ustas.RimAI.Art.TV
{
    public static class TvProgramKeyProvider
    {
        public static bool TryGetKey(Thing tvBuilding, out string key)
        {
            key = null;
            if (tvBuilding == null || tvBuilding.DestroyedOrNull()) return false;

            var loadId = tvBuilding.GetUniqueLoadID();
            if (string.IsNullOrEmpty(loadId)) return false;

            var mapId = tvBuilding.Map?.uniqueID ?? 0;
            key = $"{loadId}|{mapId}";
            return true;
        }
    }
}
