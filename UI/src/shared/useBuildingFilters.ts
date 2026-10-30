import { useMemo, useState } from "react";
import { sortZoneTypes } from "shared/zoneTypeLabels";

export interface BuildingEntry {
    prefabId: string;
    zoneType: string;
    level: number;
    lotWidth: number;
    lotDepth: number;
    heightMeters: number;
    assetPackIds: string[];
    // UIObject.m_Icon, empty string if unset - see GameBuildingCatalog.cs
    // (ResolveIconPath) for the full story. Empty means "no icon
    // available" (no Asset Icon Library installed, or it doesn't cover
    // this prefab) - render nothing, not a broken <img>, when empty.
    iconPath: string;
}

// Shared by the Phase 4 asset-picker dialog and the Phase 4b standalone
// Assets tab (UI_OVERHAUL_PLAN.md decision #2: same filter/search
// machinery, two different jobs - picking vs. browsing) - extracted out
// of the original BuildingPicker.tsx so both consumers stay in sync
// instead of carrying two copies of the same filtering logic.
//
// Every filter is a string[], not a single value (2026-10-26, user
// feedback: narrowing to e.g. Low AND Medium density in one pass, not
// five separate browsing sessions one density at a time) - empty array
// means "no filter on this field," the same meaning FilterDropdown's
// ANY sentinel used to carry for the single-select version. Level/width/
// depth are compared as strings on both sides (matching
// MultiFilterDropdown's own string-only options) rather than parsing
// back to number, since nothing here needs numeric comparison, only
// equality. Array, not Set - see MultiFilterDropdown's own comment for
// why (a Set-copying bug, confirmed in-game, silently dropped every
// value but the most recently added).
export function useBuildingFilters(buildings: BuildingEntry[]) {
    const [nameFilter, setNameFilter] = useState("");
    const [zoneTypeFilter, setZoneTypeFilter] = useState<string[]>([]);
    const [levelFilter, setLevelFilter] = useState<string[]>([]);
    const [lotWidthFilter, setLotWidthFilter] = useState<string[]>([]);
    const [lotDepthFilter, setLotDepthFilter] = useState<string[]>([]);
    const [assetPackFilter, setAssetPackFilter] = useState<string[]>([]);

    const zoneTypes = useMemo(() => sortZoneTypes(distinctStrings(buildings.map((b) => b.zoneType))), [buildings]);
    const levels = useMemo(() => distinctNumbers(buildings.map((b) => b.level)).map(String), [buildings]);
    const lotWidths = useMemo(() => distinctNumbers(buildings.map((b) => b.lotWidth)).map(String), [buildings]);
    const lotDepths = useMemo(() => distinctNumbers(buildings.map((b) => b.lotDepth)).map(String), [buildings]);
    const assetPacks = useMemo(() => distinctStrings(buildings.flatMap((b) => b.assetPackIds)), [buildings]);

    const filtered = useMemo(() => {
        const name = nameFilter.trim().toLowerCase();
        return buildings.filter((b) => {
            if (name && !b.prefabId.toLowerCase().includes(name)) return false;
            if (zoneTypeFilter.length > 0 && !zoneTypeFilter.includes(b.zoneType)) return false;
            if (levelFilter.length > 0 && !levelFilter.includes(String(b.level))) return false;
            if (lotWidthFilter.length > 0 && !lotWidthFilter.includes(String(b.lotWidth))) return false;
            if (lotDepthFilter.length > 0 && !lotDepthFilter.includes(String(b.lotDepth))) return false;
            if (assetPackFilter.length > 0 && !b.assetPackIds.some((id) => assetPackFilter.includes(id))) return false;
            return true;
        });
    }, [buildings, nameFilter, zoneTypeFilter, levelFilter, lotWidthFilter, lotDepthFilter, assetPackFilter]);

    const clearFilters = () => {
        setNameFilter("");
        setZoneTypeFilter([]);
        setLevelFilter([]);
        setLotWidthFilter([]);
        setLotDepthFilter([]);
        setAssetPackFilter([]);
    };

    return {
        filtered,
        zoneTypes,
        levels,
        lotWidths,
        lotDepths,
        assetPacks,
        nameFilter,
        setNameFilter,
        zoneTypeFilter,
        setZoneTypeFilter,
        levelFilter,
        setLevelFilter,
        lotWidthFilter,
        setLotWidthFilter,
        lotDepthFilter,
        setLotDepthFilter,
        assetPackFilter,
        setAssetPackFilter,
        clearFilters,
    };
}

function distinctStrings(values: string[]): string[] {
    return Array.from(new Set(values)).sort();
}

function distinctNumbers(values: number[]): number[] {
    return Array.from(new Set(values)).sort((a, b) => a - b);
}
