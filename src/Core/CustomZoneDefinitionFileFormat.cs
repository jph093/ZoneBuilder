using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // The JSON shape shared by FolderCustomZoneDefinitionLibrary's own
    // files and LegacyCustomZoneDefinitionsMigrator - same split as
    // CollectionFileFormat/FolderCollectionLibrary, for the same reason
    // (see ARCHITECTURE.md).
    internal static class CustomZoneDefinitionFileFormat
    {
        public const int CurrentSchemaVersion = 1;

        // No real schema change has ever happened to this format yet -
        // empty on purpose. See CollectionFileFormat.Migrations for the
        // pattern to follow the next time CurrentSchemaVersion is bumped.
        private static readonly IReadOnlyList<SchemaMigrationStep> Migrations = Array.Empty<SchemaMigrationStep>();

        // The one place every load path (FolderCustomZoneDefinitionLibrary,
        // LegacyCustomZoneDefinitionsMigrator) should parse a zone
        // definitions file from - see CollectionFileFormat.ParseLibraryFile
        // for why.
        public static LibraryFileDto ParseLibraryFile(string json)
        {
            json = SchemaMigration.Migrate(json, CurrentSchemaVersion, Migrations);
            return JsonConvert.DeserializeObject<LibraryFileDto>(json);
        }

        public sealed class LibraryFileDto
        {
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;
            public List<CustomZoneDefinitionDto> Definitions { get; set; } = new List<CustomZoneDefinitionDto>();
        }

        public sealed class CustomZoneDefinitionDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public ZoneType BaseZoneType { get; set; }
            // Explicit default (not the enum's own zero-value), so an
            // existing library file saved before this field existed
            // deserializes every zone as Europe - matching the behavior
            // they actually had (GameZonePrefabResolver picked whatever
            // its entity query happened to return first, observed to
            // always be an EU-themed candidate) - not silently
            // reinterpreting them as NorthAmerica just because that
            // happens to be ZoneRegion's first declared value.
            public ZoneRegion Region { get; set; } = ZoneRegion.Europe;
            // Null by default (same as the C# property it mirrors) for a
            // file saved before this field existed - CustomZoneBuilder
            // treats that as "not backfilled yet" and resolves/persists a
            // real value the next time this zone is built. No schema
            // bump/migration needed, same precedent as Region above: a
            // purely additive field Newtonsoft just leaves at its DTO
            // default when absent from the JSON.
            public string VanillaZoneName { get; set; }
            public List<Guid> CollectionIds { get; set; } = new List<Guid>();
        }

        public static CustomZoneDefinitionDto ToDto(CustomZoneDefinition definition)
        {
            return new CustomZoneDefinitionDto
            {
                Id = definition.Id,
                Name = definition.Name,
                BaseZoneType = definition.BaseZoneType,
                Region = definition.Region,
                VanillaZoneName = definition.VanillaZoneName,
                CollectionIds = definition.CollectionIds.ToList(),
            };
        }

        public static CustomZoneDefinition FromDto(CustomZoneDefinitionDto dto)
        {
            var definition = new CustomZoneDefinition(dto.Id, dto.Name, dto.BaseZoneType, dto.Region, dto.VanillaZoneName);
            foreach (var collectionId in dto.CollectionIds ?? Enumerable.Empty<Guid>())
                definition.AddCollection(collectionId);
            return definition;
        }
    }
}
