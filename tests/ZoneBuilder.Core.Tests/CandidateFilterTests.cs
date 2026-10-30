using System.Collections.Generic;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class CandidateFilterTests
    {
        private static readonly LotSize TwoByFour = new LotSize(2, 4);

        [Fact]
        public void ExcludesBuildingsWithWrongZoneType()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("house1", ZoneType.ResidentialLow, 1, TwoByFour),
                new BuildingCandidate("shop1", ZoneType.CommercialLow, 1, TwoByFour),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "house1" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void ExcludesBuildingsTooDeepForTheLot()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("fits", ZoneType.ResidentialLow, 1, new LotSize(2, 3)),
                new BuildingCandidate("tooDeep", ZoneType.ResidentialLow, 1, new LotSize(2, 5)),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "fits" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void IncludesAdoptedAssetsUsingTheirOwnSettings()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new AdoptedAssetEntry("adopted-house", new AdoptedAssetSettings(ZoneType.ResidentialLow, 1, TwoByFour, householdCount: 3)));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "adopted-house" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void DedupesTheSamePrefabReachedThroughMultipleEntries()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("house1", ZoneType.ResidentialLow, 1, TwoByFour),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            collection.Add(new ExplicitAssetEntry("house1"));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Single(result);
        }

        [Fact]
        public void NoFallbackReturnsEmptyWhenNothingMatches()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);
            var vanilla = new[] { new BuildingCandidate("vanilla-house", ZoneType.ResidentialLow, 1, TwoByFour) };

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None, vanilla);

            Assert.Empty(result);
        }

        [Fact]
        public void VanillaZoneFallbackIsUsedWhenNothingMatches()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);
            var vanilla = new[] { new BuildingCandidate("vanilla-house", ZoneType.ResidentialLow, 1, TwoByFour) };

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.VanillaZone, vanilla);

            Assert.Equal(new[] { "vanilla-house" }, result.Select(c => c.PrefabId));
        }

        // Regression guard (2026-10-29): a VanillaZoneName (exact vanilla
        // zone-prefab identity) based version of Matches was tried and
        // reverted the same day - confirmed in-game on a heavily-modded
        // playset, it rejected nearly every cross-pack building, since a
        // Collection deliberately mixes buildings from many different
        // asset packs under one coarse category (see Core.Collection's
        // own comment) and each pack typically registers its own distinct
        // vanilla zone prefab for "the same" category. Two candidates
        // here carry DIFFERENT VanillaZoneName values (simulating two
        // different asset packs) but the SAME ZoneType - both must still
        // be included, matching purely on the coarse category.
        [Fact]
        public void IncludesCrossPackBuildingsWithDifferentVanillaZoneNames()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("packAHouse", ZoneType.ResidentialMedium, 1, TwoByFour, vanillaZoneName: "Pack A Medium Density"),
                new BuildingCandidate("packBHouse", ZoneType.ResidentialMedium, 1, TwoByFour, vanillaZoneName: "Pack B Medium Density"),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialMedium));
            var lot = new LotQuery(ZoneType.ResidentialMedium, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "packAHouse", "packBHouse" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void IncludesAdoptedAssetsMatchingByZoneTypeOnly()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new AdoptedAssetEntry("adopted-house", new AdoptedAssetSettings(ZoneType.ResidentialMedium, 1, TwoByFour)));
            var lot = new LotQuery(ZoneType.ResidentialMedium, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "adopted-house" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void VanillaSelectorCanFilterByAssetPack()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("packHouse", ZoneType.ResidentialLow, 1, TwoByFour, assetPackId: "NA"),
                new BuildingCandidate("otherHouse", ZoneType.ResidentialLow, 1, TwoByFour, assetPackId: "EU"),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow, assetPackId: "NA"));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "packHouse" }, result.Select(c => c.PrefabId));
        }
    }
}
