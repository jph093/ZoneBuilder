// Mirrors Core.ZoneType's real categories (src/Core/Primitives.cs), but in
// the order a player actually expects - grouped by category, low-to-high
// density within each - not Core.ZoneType's own declaration order, where
// OfficeLow/OfficeHigh are appended at the end to keep existing saved
// zones' serialized enum ordinal stable (see that file's own comment).
// "Office" (no suffix) is deliberately omitted here - it's only ever a
// legacy BaseZoneType value from a zone saved before 2026-10-13's density
// fix, never produced by new data, so it has no place to sort into or
// label for going forward.
// ResidentialMixedUse/ResidentialLowRent/ResidentialRowHouse sit right
// after ResidentialHigh, not off at the end with Office/Industrial -
// they're peers of Low/Medium/High (a player plans a Mixed Use, Low
// Rent, or Row Housing district for a different purpose entirely, the
// same way Low/Medium/High already differ - user feedback, 2026-11-01,
// reiterated for Row Housing 2026-10-30), not an afterthought, even
// though Core.ZoneType's own declaration order has them last (ordinal-
// persistence reasons that don't apply to this display-only order - see
// that file's own comment).
export const ZONE_TYPE_ORDER = ["ResidentialLow", "ResidentialMedium", "ResidentialHigh", "ResidentialMixedUse", "ResidentialLowRent", "ResidentialRowHouse", "CommercialLow", "CommercialHigh", "OfficeLow", "OfficeHigh", "Industrial"] as const;

const ZONE_TYPE_LABELS: Record<string, string> = {
    ResidentialLow: "Residential Low",
    ResidentialMedium: "Residential Medium",
    ResidentialHigh: "Residential High",
    ResidentialMixedUse: "Residential Mixed Use",
    ResidentialLowRent: "Residential Low Rent",
    ResidentialRowHouse: "Residential Row Housing",
    CommercialLow: "Commercial Low",
    CommercialHigh: "Commercial High",
    OfficeLow: "Office Low",
    OfficeHigh: "Office High",
    Industrial: "Industrial",
};

// Falls back to the raw value for anything unrecognized (e.g. legacy
// "Office") rather than hiding it - an old zone with that BaseZoneType
// should still display as *something* meaningful, not disappear.
export const formatZoneType = (zoneType: string): string => ZONE_TYPE_LABELS[zoneType] ?? zoneType;

export const sortZoneTypes = (zoneTypes: string[]): string[] => {
    const orderIndex = new Map(ZONE_TYPE_ORDER.map((type, index) => [type as string, index]));
    return [...zoneTypes].sort((a, b) => (orderIndex.get(a) ?? Number.MAX_SAFE_INTEGER) - (orderIndex.get(b) ?? Number.MAX_SAFE_INTEGER));
};
