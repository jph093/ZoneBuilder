using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // Plain DTO for the UI, mirrors CollectionUIEntry's role for
    // CustomZoneDefinition. collectionIds (not just a count) is needed so
    // the attach/detach panel can show which collections are already
    // attached to this zone.
    public readonly struct ZoneUIEntry : IJsonWritable
    {
        private readonly string m_Id;
        private readonly string m_Name;
        private readonly string m_BaseZoneType;
        private readonly string m_Region;
        private readonly string[] m_CollectionIds;
        // How many buildings this zone currently has already grown -
        // CustomZoneBuilder.CountPlacedInstances, refreshed whenever this
        // list is (zone create/delete/rename/attach/detach, and once on
        // game load) - see that method's own comment for why it can't be
        // computed live, continuously, for every zone without a real
        // performance cost on a heavily-modded save. Lets the delete
        // confirmation warn accurately that deleting this zone will also
        // remove these - see RetireZone's own comment for why that's now
        // necessary, not optional.
        private readonly int m_PlacedBuildingCount;

        public ZoneUIEntry(string id, string name, string baseZoneType, string region, string[] collectionIds, int placedBuildingCount)
        {
            m_Id = id;
            m_Name = name;
            m_BaseZoneType = baseZoneType;
            m_Region = region;
            m_CollectionIds = collectionIds;
            m_PlacedBuildingCount = placedBuildingCount;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("id");
            writer.Write(m_Id);
            writer.PropertyName("name");
            writer.Write(m_Name);
            writer.PropertyName("baseZoneType");
            writer.Write(m_BaseZoneType);
            writer.PropertyName("region");
            writer.Write(m_Region);
            writer.PropertyName("collectionIds");
            writer.ArrayBegin((uint)m_CollectionIds.Length);
            foreach (var id in m_CollectionIds)
                writer.Write(id);
            writer.ArrayEnd();
            writer.PropertyName("placedBuildingCount");
            writer.Write(m_PlacedBuildingCount);
            writer.TypeEnd();
        }
    }
}
