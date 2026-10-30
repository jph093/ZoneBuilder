using Colossal.Entities;
using Game.Prefabs;
using Game.Simulation;
using Game.Zones;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using CoreZoneType = ZoneBuilder.Core.ZoneType;

namespace ZoneBuilder.CustomZones.GameAdapters
{
    // Resolves ZonePrefabs against the live game - either a vanilla zone
    // matching a Core.ZoneType category (to use as a custom zone's
    // simulation template), or an already-registered zone by its exact
    // prefab name (to find a custom zone CustomZoneBuilder built in an
    // earlier request, for rebuild). Kept separate from GameBuildingCatalog
    // since zones and buildings are different prefab kinds with different
    // resolution needs, even though TryFindVanillaZone reuses its
    // AreaType/IsOffice/density -> Core.ZoneType mapping rather than
    // duplicating it.
    public sealed class GameZonePrefabResolver
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly EntityManager m_EntityManager;
        private readonly EntityQuery m_ZoneQuery;
        private readonly EntityQuery m_BuildingQuery;

        // Lazily-built, never-invalidated cache backing IsRowHousingZone -
        // see that method's own comment. Null means "not built yet."
        // Keyed by the zone entity (SpawnableBuildingData.m_ZonePrefab on
        // every building that points at it), value is "every building
        // seen so far for this zone has had a lot width of 2 or less."
        private Dictionary<Entity, bool> m_RowHousingZones;

        public GameZonePrefabResolver(World world)
        {
            m_EntityManager = world.EntityManager;
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>());
            m_BuildingQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<SpawnableBuildingData>(),
                ComponentType.ReadOnly<PrefabData>());
        }

        // The actual Core.ZoneType resolution every caller should use -
        // GameBuildingCatalog.TryMapToCoreZoneType (static) only computes
        // the density/Mixed-Use/Low-Rent bucket from a zone's OWN data;
        // it can never produce ResidentialRowHouse, because there is no
        // authoritative, data-driven signal for it on the zone alone (see
        // this method's own history - two prior attempts both confirmed
        // wrong in-game 2026-10-30: ZoneData.m_ZoneFlags.SupportNarrow
        // read directly is set at RUNTIME by BuildingInitializeSystem on
        // whichever zone a narrow building happens to register under, not
        // authored identity; promoting that same flag across every zone
        // sharing a UIObjectData.m_Group failed differently - that field
        // turned out to be a COARSE bucket (confirmed via DECISIONS.md's
        // own prior diagnostic: our cloned zone's group is literally
        // named "ZonesResidential," one bucket for every residential
        // density tier) and swallowed Low/Medium/High entirely into Row
        // Housing).
        //
        // This uses the actual mechanism the popular "Find It" mod
        // (Paradox Mods id 77240, 755k+ subscribers - decompiled
        // 2026-10-30 specifically to answer "how does a working mod
        // isolate this") uses to tell Row Housing apart from plain Medium
        // Density: FindIt.Systems.PrefabIndexingSystem.IndexZones computes
        // each zone's density from m_ResidentialProperties/m_SpaceMultiplier
        // (matching ZoneProperties's own authoring tooltip exactly), and
        // for the ambiguous "ratio < 1" range - plain Medium Density and
        // Row Housing both land there - disambiguates by scanning every
        // building actually registered to that zone
        // (SpawnableBuildingData.m_ZonePrefab) and checking whether ALL of
        // them have a lot width of 2 or less (BuildingData.m_LotSize.x),
        // not just whether any one of them does. That "ALL, not ANY" is
        // exactly what SupportNarrow lacked and why it kept misfiring:
        // real Row Housing's own narrow-lot-only constraint makes this
        // true for it unavoidably, while a genuine Medium zone keeps
        // plenty of wider buildings in its own roster even after picking
        // up one stray narrow building, so it correctly stays Medium.
        public bool TryMapToCoreZoneType(Entity zoneEntity, ZoneData zoneData, ZonePropertiesData zoneProps, out CoreZoneType zoneType)
        {
            if (!GameBuildingCatalog.TryMapToCoreZoneType(zoneData, zoneProps, out zoneType))
                return false;

            // Low Rent's own authored signal (m_IgnoreLandValue) was never
            // actually confirmed against live data (DECISIONS.md, from
            // when Mixed Use/Low Rent were first split out: "not yet
            // confirmed... the fields' own authoring intent is solid
            // evidence, not yet a live-game count") - and user feedback
            // 2026-10-30 confirms it's empty in practice (no building
            // anywhere resolves to it). A first fallback attempt (name-
            // substring matching on genuinely-vanilla zones, same session)
            // ALSO came back empty - the real zone's internal name doesn't
            // literally contain "Low Rent" either.
            //
            // Found instead via Game.Prefabs.GroupAmbience/GroupAmbienceData
            // - an entirely different, OPTIONAL authored zone-level
            // component (confirmed via decompiled source: [ComponentMenu
            // ("Zones/", ...)], IZoneBuildingComponent, same authoring
            // pattern as ZoneProperties) that tags a zone with a
            // Game.Simulation.GroupAmbienceType for audio/ambience
            // purposes - NOT the same system as ZonePropertiesData at all,
            // just a second, independent place CO's own data happens to
            // encode a zone's real category. That enum has dedicated
            // ResidentialLow/Medium/High/Mixed/LowRent values (mirroring
            // Core.ZoneType almost exactly) but notably NO Row value -
            // consistent with every other finding this session that Row
            // Housing has no authored identity signal anywhere, while
            // Mixed Use/Low Rent do. Mixed Use is NOT given this same
            // fallback: m_AllowedSold is already confirmed working
            // (buildings resolve to it correctly today), so there's
            // nothing broken to patch over.
            if (IsPlainDensityBucket(zoneType) && HasAmbienceType(zoneEntity, GroupAmbienceType.ResidentialLowRent))
            {
                zoneType = CoreZoneType.ResidentialLowRent;
                return true;
            }

            // Mirroring FindIt's own branch exactly - it only ever
            // considers Row Housing within its "ratio < 1" range, which is
            // this method's ResidentialMedium case; Low and High never
            // reach that branch in its own code either.
            if (zoneType == CoreZoneType.ResidentialMedium && IsRowHousingZone(zoneEntity))
                zoneType = CoreZoneType.ResidentialRowHouse;

            return true;
        }

        private static bool IsPlainDensityBucket(CoreZoneType zoneType) =>
            zoneType == CoreZoneType.ResidentialLow || zoneType == CoreZoneType.ResidentialMedium || zoneType == CoreZoneType.ResidentialHigh;

        private bool HasAmbienceType(Entity zoneEntity, GroupAmbienceType ambienceType)
        {
            return m_EntityManager.TryGetComponent<GroupAmbienceData>(zoneEntity, out var ambience) && ambience.m_AmbienceType == ambienceType;
        }

        private bool IsRowHousingZone(Entity zoneEntity)
        {
            EnsureRowHousingZones();
            return m_RowHousingZones.TryGetValue(zoneEntity, out var allNarrow) && allNarrow;
        }

        private void EnsureRowHousingZones()
        {
            if (m_RowHousingZones != null)
                return;

            m_RowHousingZones = new Dictionary<Entity, bool>();
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    // Same exclusion FindIt itself applies (WithNone<SignatureBuildingData>())
                    // and the same one GameBuildingCatalog.TryDescribeBuilding
                    // already relies on elsewhere in this codebase - a
                    // Signature building's lot size says nothing about its
                    // zone's ordinary building roster.
                    if (m_EntityManager.HasComponent<SignatureBuildingData>(buildingEntity))
                        continue;

                    var spawnable = m_EntityManager.GetComponentData<SpawnableBuildingData>(buildingEntity);
                    var buildingData = m_EntityManager.GetComponentData<BuildingData>(buildingEntity);
                    var isNarrow = buildingData.m_LotSize.x <= 2;

                    m_RowHousingZones[spawnable.m_ZonePrefab] = m_RowHousingZones.TryGetValue(spawnable.m_ZonePrefab, out var allNarrowSoFar)
                        ? allNarrowSoFar && isNarrow
                        : isNarrow;
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }
        }

        // Picks a registered vanilla zone matching the given category AND
        // region - region is purely which UIGroupPrefab the clone displays
        // under (see Core.ZoneRegion), not a different simulation profile.
        // Only ever picks a GENUINELY VANILLA zone - never a mod/asset
        // pack's own zone prefab, even one sharing the same category (user
        // feedback, 2026-11-01: silently landing on a third-party mod's
        // zone is worse than failing loudly - see GetRank's own comment
        // for how "vanilla" is determined). If no vanilla zone matches
        // this category AND region at all, this simply fails
        // (TryGetOrCloneZone logs "No vanilla zone found..." and the zone
        // isn't built) - every one of the 11 standard categories
        // (Residential Low/Medium/High/Mixed Use/Low Rent/Row House,
        // Commercial Low/High, Office Low/High, Industrial) is core
        // base-game functionality, not DLC-gated, so this should never
        // actually happen for real vanilla content; if it does, that's a
        // real problem worth surfacing, not papering over with a
        // substitute.
        public bool TryFindVanillaZone(CoreZoneType zoneType, Core.ZoneRegion region, out ZonePrefab prefab)
        {
            var entities = m_ZoneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                var assetPrefix = GetAssetPrefix(region);
                ZonePrefab best = null;
                var bestRank = -1;

                foreach (var entity in entities)
                {
                    if (!m_EntityManager.TryGetComponent<ZoneData>(entity, out var zoneData) ||
                        !m_EntityManager.TryGetComponent<ZonePropertiesData>(entity, out var zoneProps))
                        continue;

                    if (!TryMapToCoreZoneType(entity, zoneData, zoneProps, out var candidateZoneType) || candidateZoneType != zoneType)
                        continue;

                    if (!m_PrefabSystem.TryGetPrefab<ZonePrefab>(entity, out var candidate))
                        continue;

                    var rank = GetRank(entity, candidate, assetPrefix);
                    if (rank < 0)
                        continue;

                    if (rank > bestRank)
                    {
                        best = candidate;
                        bestRank = rank;
                        if (rank == 1)
                            break; // Can't do better than a themed vanilla match.
                    }
                }

                prefab = best;
                return best != null;
            }
            finally
            {
                entities.Dispose();
            }
        }

        private static string GetAssetPrefix(Core.ZoneRegion region) => region == Core.ZoneRegion.NorthAmerica ? "NA" : "EU";

        // Ranking helper for TryFindVanillaZone. Negative means "never a
        // candidate" - a mod/asset pack's own
        // zone prefab (confirmed via decompiled Game.UI.InGame.
        // ToolbarUISystem: it uses this exact "has no AssetPackElement
        // buffer at all" check for its own "vanilla only" zoning-tool
        // filter, and every mod/DLC/Content Creator Pack registers at
        // least one AssetPackElement - same signal GameBuildingCatalog.
        // ResolveAssetPackIds already relies on for buildings). Among
        // genuinely vanilla candidates: 1 for a themed match (ThemeObject.
        // m_Theme.assetPrefix matching the requested region's "NA"/"EU" -
        // confirmed real via live reflection against Game.dll), 0
        // otherwise (vanilla but the other theme - covers a category that
        // only ships one vanilla variant).
        private int GetRank(Entity entity, ZonePrefab candidate, string assetPrefix)
        {
            var isVanilla = !m_EntityManager.TryGetBuffer<AssetPackElement>(entity, true, out var packs) || packs.Length == 0;
            if (!isVanilla)
                return -1;

            var isThemed = candidate.TryGet<ThemeObject>(out var themeObject) &&
                themeObject.m_Theme != null &&
                string.Equals(themeObject.m_Theme.assetPrefix, assetPrefix, System.StringComparison.OrdinalIgnoreCase);

            return isThemed ? 1 : 0;
        }

        // Exact-name lookup, same technique as GameBuildingCatalog.TryGetBuildingPrefab
        // (iterate + match PrefabBase.name, not PrefabID - see its own
        // comment for why).
        public bool TryFindByName(string name, out ZonePrefab prefab, out Entity entity)
        {
            var entities = m_ZoneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var candidateEntity in entities)
                {
                    if (!m_PrefabSystem.TryGetPrefab<ZonePrefab>(candidateEntity, out var candidatePrefab) || candidatePrefab.name != name)
                        continue;

                    prefab = candidatePrefab;
                    entity = candidateEntity;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            prefab = null;
            entity = Entity.Null;
            return false;
        }
    }
}
