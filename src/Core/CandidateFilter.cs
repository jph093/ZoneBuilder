using System.Collections.Generic;
using System.Linq;

namespace ZoneBuilder.Core
{
    public readonly struct LotQuery
    {
        public ZoneType ZoneType { get; }
        public LotSize LotSize { get; }

        public LotQuery(ZoneType zoneType, LotSize lotSize)
        {
            ZoneType = zoneType;
            LotSize = lotSize;
        }
    }

    public enum FallbackPolicy
    {
        // No matching building in the collection(s) means nothing spawns.
        None,
        // Fall back to vanilla's own candidates for the lot's zone.
        VanillaZone,
    }

    // Given a lot and the collection(s) in scope for it, which buildings are
    // eligible? Pure and game-independent so it's unit-testable without the
    // game running - see ARCHITECTURE.md.
    public static class CandidateFilter
    {
        public static IReadOnlyList<BuildingCandidate> GetEligibleBuildings(
            LotQuery lot,
            IEnumerable<Collection> collections,
            IBuildingCatalog catalog,
            FallbackPolicy fallbackPolicy,
            IEnumerable<BuildingCandidate> vanillaCandidates = null)
        {
            var resolved = new List<BuildingCandidate>();

            foreach (var collection in collections)
            {
                foreach (var entry in collection.Entries)
                {
                    switch (entry)
                    {
                        case VanillaSelectorEntry selector:
                            resolved.AddRange(catalog.Resolve(selector));
                            break;
                        case ExplicitAssetEntry explicitEntry:
                            resolved.Add(catalog.Resolve(explicitEntry));
                            break;
                        case AdoptedAssetEntry adoptedEntry:
                            resolved.Add(catalog.Resolve(adoptedEntry));
                            break;
                    }
                }
            }

            var eligible = DistinctByPrefab(resolved.Where(c => Matches(c, lot)));

            if (eligible.Count == 0 && fallbackPolicy == FallbackPolicy.VanillaZone && vanillaCandidates != null)
                eligible = vanillaCandidates.Where(c => Matches(c, lot)).ToList();

            return eligible;
        }

        private static List<BuildingCandidate> DistinctByPrefab(IEnumerable<BuildingCandidate> candidates)
        {
            return candidates.GroupBy(c => c.PrefabId).Select(g => g.First()).ToList();
        }

        // Width must match the lot exactly (buildings face the street at a
        // fixed width); depth only needs to fit. Unverified against actual
        // game lot-matching rules - see DECISIONS.md.
        //
        // Plain CoreZoneType comparison - a VanillaZoneName (exact vanilla
        // zone-prefab identity) variant of this was tried and reverted
        // the same day (confirmed in-game, a heavily-modded playset): a
        // Collection deliberately mixes buildings from many different
        // asset packs under one coarse category (see Core.Collection's
        // own comment), and each pack typically registers its own
        // distinct vanilla zone prefab for "the same" category, so exact-
        // identity matching rejected nearly everything cross-pack. See
        // CustomZoneBuilder.IsCompatible's own comment for the full
        // writeup.
        private static bool Matches(BuildingCandidate candidate, LotQuery lot)
        {
            return candidate.ZoneType == lot.ZoneType
                && candidate.LotSize.Width == lot.LotSize.Width
                && candidate.LotSize.Depth <= lot.LotSize.Depth;
        }
    }
}
