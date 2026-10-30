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

        // Same "peer of Low/Medium/High, not a variant" reasoning as
        // Mixed Use/Low Rent above, appended the same way for the same
        // ordinal-persistence reason - user feedback (2026-10-30): row
        // housing was missed when those two were split out, even though
        // it's the identical category of gap. Confirmed real and
        // distinct via the CS2 wiki ("Medium density row housing
        // consists of narrow wall-to-wall buildings") and Paradox's own
        // feature page, which lists it as its own residential zone type
        // alongside Mixed Use/Low Rent/plain density - not a guess, but
        // unlike those two, no dedicated ZonePropertiesData field exists
        // for it, and - unlike those two - no OTHER signal on the zone's
        // OWN data distinguishes it from plain Medium Density either.
        // Two attempts tried and ruled out in-game, same day:
        // ZoneData.m_ZoneFlags.SupportNarrow read directly (it's set at
        // RUNTIME by Game.Prefabs.BuildingInitializeSystem on whichever
        // zone a narrow building happens to register under - load-order/
        // install-dependent, not stable identity); and that same flag
        // promoted across every zone sharing a UIObjectData.m_Group
        // (confirmed, via this project's own prior Platter-compatibility
        // diagnostic in DECISIONS.md, to group at a COARSE level: our own
        // cloned zone's group is literally named "ZonesResidential," one
        // bucket for every residential density tier, not one per
        // zoning-tool button - so promoting a whole group swallowed
        // Low/Medium/High entirely into Row Housing).
        //
        // Settled by decompiling the popular "Find It" mod (Paradox Mods
        // id 77240, 755k+ subscribers, which has a working Row Houses
        // filter) to see how a mod that actually gets this right does it:
        // FindIt.Systems.PrefabIndexingSystem.IndexZones scans every
        // building actually registered to a zone and checks whether ALL
        // of them have a lot width of 2 or less - not whether the zone
        // itself carries any flag. That "ALL, not ANY" over the zone's
        // real building roster is what SupportNarrow lacked. See
        // GameZonePrefabResolver.TryMapToCoreZoneType's own comment for
        // the full mechanism and history.
        ResidentialRowHouse,
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
