namespace ZoneBuilder.Core
{
    // A plain, game-independent description of a building that could spawn on
    // a lot - resolved from a CollectionEntry by an IBuildingCatalog.
    public sealed class BuildingCandidate
    {
        public string PrefabId { get; }
        public ZoneType ZoneType { get; }
        public int Level { get; }
        public LotSize LotSize { get; }
        public string AssetPackId { get; }

        // The exact vanilla zone prefab this building is authored for
        // (its own registered name), not just the coarse ZoneType bucket -
        // see CustomZoneDefinition.VanillaZoneName's own comment for why
        // the bucket alone isn't a reliable compatibility key. Null for
        // an AdoptedAssetEntry-resolved candidate (a user-declared, not
        // game-resolved, building with no real vanilla zone to anchor to).
        public string VanillaZoneName { get; }

        public BuildingCandidate(string prefabId, ZoneType zoneType, int level, LotSize lotSize, string assetPackId = null, string vanillaZoneName = null)
        {
            PrefabId = prefabId;
            ZoneType = zoneType;
            Level = level;
            LotSize = lotSize;
            AssetPackId = assetPackId;
            VanillaZoneName = vanillaZoneName;
        }
    }
}
