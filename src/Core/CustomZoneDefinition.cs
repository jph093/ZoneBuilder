using System;
using System.Collections.Generic;

namespace ZoneBuilder.Core
{
    // A persisted, user-defined custom zone: a base vanilla zone category
    // plus the Collection(s) whose buildings it should offer. Identity is
    // Id, not Name - same rename-free principle as Collection, and for the
    // same reason: the in-game zone/building prefabs this eventually builds
    // need a stable name derived from Id, not from the user-facing Name, so
    // renaming a definition later doesn't orphan what was already built -
    // see DECISIONS.md.
    public sealed class CustomZoneDefinition
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public ZoneType BaseZoneType { get; set; }

        // Which vanilla theme variant this zone's simulation template was
        // (or will be) cloned from - see Core.ZoneRegion for what this
        // does and doesn't affect. Defaults to Europe, matching the
        // behavior every zone had before this existed (GameZonePrefabResolver
        // picked whatever candidate its entity query happened to return
        // first, which was observed to always be an EU-themed one).
        public ZoneRegion Region { get; set; }

        // The exact vanilla zone prefab (its own registered name, e.g.
        // "NA Residential Medium Density" vs a distinct "NA Mixed
        // Housing") this zone's simulation template was actually cloned
        // from - set once, at whichever build first resolves it, and
        // never re-decided after (see CustomZoneBuilder.TryGetOrCloneZone's
        // own comment). BaseZoneType alone is too coarse to be that
        // identity: it only captures AreaType + a 3-tier density bucket
        // (GameBuildingCatalog.TryMapToCoreZoneType), and more than one
        // distinct, non-interchangeable vanilla zone prefab can share one
        // bucket (confirmed via the CS2 wiki's own zone table and
        // Platter's exact-identity zone cache - see DECISIONS.md "Exact
        // vanilla zone identity, not coarse density buckets"). Null for
        // every zone saved before this field existed - CustomZoneBuilder
        // backfills it the first time such a zone goes through template
        // resolution again, exactly once, so it becomes stable from then
        // on instead of being silently re-decided on every load the way
        // BaseZoneType's own bucket lookup always was.
        public string VanillaZoneName { get; set; }

        private readonly List<Guid> m_CollectionIds = new List<Guid>();
        public IReadOnlyList<Guid> CollectionIds => m_CollectionIds;

        public CustomZoneDefinition(string name, ZoneType baseZoneType, ZoneRegion region = ZoneRegion.Europe, string vanillaZoneName = null)
            : this(Guid.NewGuid(), name, baseZoneType, region, vanillaZoneName)
        {
        }

        public CustomZoneDefinition(Guid id, string name, ZoneType baseZoneType, ZoneRegion region = ZoneRegion.Europe, string vanillaZoneName = null)
        {
            Id = id;
            Name = name;
            BaseZoneType = baseZoneType;
            Region = region;
            VanillaZoneName = vanillaZoneName;
        }

        public void AddCollection(Guid collectionId)
        {
            if (!m_CollectionIds.Contains(collectionId))
                m_CollectionIds.Add(collectionId);
        }

        public void RemoveCollection(Guid collectionId)
        {
            m_CollectionIds.Remove(collectionId);
        }

        // Shallow copy (CollectionIds are Guids, value types - a shallow
        // copy of the list is a true independent copy). Exists so
        // ICustomZoneDefinitionLibrary.TryGet can hand out a value the
        // caller can freely mutate without that mutation silently
        // touching the library's own stored state before Save is called -
        // see Collection.Clone's identical comment for the full reasoning.
        public CustomZoneDefinition Clone()
        {
            var clone = new CustomZoneDefinition(Id, Name, BaseZoneType, Region, VanillaZoneName);
            foreach (var collectionId in m_CollectionIds)
                clone.AddCollection(collectionId);
            return clone;
        }
    }
}
