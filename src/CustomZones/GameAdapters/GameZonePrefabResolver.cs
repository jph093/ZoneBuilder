using Colossal.Entities;
using Game.Prefabs;
using Game.Zones;
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

        public GameZonePrefabResolver(World world)
        {
            m_EntityManager = world.EntityManager;
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>());
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
        // isn't built) - every one of the 10 standard categories
        // (Residential Low/Medium/High/Mixed Use/Low Rent, Commercial
        // Low/High, Office Low/High, Industrial) is core base-game
        // functionality, not DLC-gated, so this should never actually
        // happen for real vanilla content; if it does, that's a real
        // problem worth surfacing, not papering over with a substitute.
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

                    if (!GameBuildingCatalog.TryMapToCoreZoneType(zoneData, zoneProps, out var candidateZoneType) || candidateZoneType != zoneType)
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
