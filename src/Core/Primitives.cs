namespace ZoneBuilder.Core
{
    // Mirrors the vanilla zone categories - membership confirmed against
    // real building names (e.g. `OfficeLow01_L1_3x4`, see DECISIONS.md) and
    // GameBuildingCatalog.TryMapToCoreZoneType's decompiled-source-verified
    // AreaType/IsOffice/density mapping.
    //
    // OfficeLow/OfficeHigh are appended at the end, not alongside Office
    // where a reader would expect them - this enum is persisted directly
    // (Newtonsoft, no StringEnumConverter, so by ordinal) in every saved
    // CustomZoneDefinition's BaseZoneType. Inserting new members earlier
    // would silently shift the ordinal of Office/Industrial and corrupt
    // any already-saved zone using either. Office itself is kept
    // (never produced by new data after 2026-10-13 - see DECISIONS.md -
    // but still a valid BaseZoneType for anything saved before that).
    public enum ZoneType
    {
        ResidentialLow,
        ResidentialMedium,
        ResidentialHigh,
        CommercialLow,
        CommercialHigh,
        Office,
        Industrial,
        OfficeLow,
        OfficeHigh,

        // Appended, not inserted alongside ResidentialHigh where a reader
        // would expect them - same ordinal-persistence reasoning as
        // OfficeLow/OfficeHigh above. These are PEERS of ResidentialLow/
        // Medium/High, not variants of one of them - user feedback
        // (2026-11-01): a player plans a Mixed Use or Low Rent district
        // for an entirely different purpose than a plain density choice,
        // the same way Low/Medium/High already serve different purposes,
        // and expects to pick it directly rather than have the mod
        // silently decide it on their behalf within some other category.
        // Confirmed real, distinct vanilla categories (not density tiers
        // of ResidentialMedium/High) via the CS2 wiki's own zone table
        // (different household-per-tile stats from either) and two
        // dedicated ZonePropertiesData fields that exist for exactly this
        // (see GameBuildingCatalog.TryMapToCoreZoneType's own comment) -
        // not a guess. See DECISIONS.md "Exact vanilla zone identity, not
        // coarse density buckets".
        ResidentialMixedUse,
        ResidentialLowRent,
    }

    // Which vanilla theme a custom zone's simulation template is cloned
    // from - purely a UI/catalog grouping choice (which UIGroupPrefab the
    // clone displays under in the zone-painting tool, mirroring
    // Game.Prefabs.ThemePrefab.assetPrefix - confirmed real via live
    // reflection against Game.dll, not guessed), not a change to
    // simulation behavior or which buildings spawn - that's entirely
    // controlled by the zone's attached Collections regardless of this
    // choice. See GameAdapters/GameZonePrefabResolver.cs and DECISIONS.md.
    public enum ZoneRegion
    {
        NorthAmerica,
        Europe,
    }

    public readonly struct LotSize
    {
        public int Width { get; }
        public int Depth { get; }

        public LotSize(int width, int depth)
        {
            Width = width;
            Depth = depth;
        }
    }
}
