import { MultiFilterDropdown } from "shared/MultiFilterDropdown";
import { TextInput } from "shared/TextInput";
import { TextButton } from "shared/TextButton";
import { useBuildingFilters } from "shared/useBuildingFilters";
import { formatZoneType } from "shared/zoneTypeLabels";
import styles from "shared/BuildingFilterBar.module.scss";

// The render half of useBuildingFilters - split out so both the Phase 4
// picker dialog and the Phase 4b Assets tab render an identical filter
// bar, not two hand-copied versions of the same markup. Search gets its
// own row (2026-10-11, user feedback in-game: it was too constrained
// sharing a row with five dropdowns + Clear) - the dropdowns/Clear sit
// on a second row below it. Each dropdown is multi-select
// (MultiFilterDropdown, 2026-10-26) - see useBuildingFilters's own
// comment for why every filter is a plain string[] now.
export const BuildingFilterBar = (filters: ReturnType<typeof useBuildingFilters>) => (
    <div className={styles.wrapper}>
        <TextInput value={filters.nameFilter} onChange={filters.setNameFilter} placeholder="Search name" className={styles.searchInput} fontSize="1.5em" />
        <div className={styles.filterRow}>
            <MultiFilterDropdown label="Any zone" options={filters.zoneTypes} selected={filters.zoneTypeFilter} onChange={filters.setZoneTypeFilter} formatOption={formatZoneType} />
            <MultiFilterDropdown label="Any level" options={filters.levels} selected={filters.levelFilter} onChange={filters.setLevelFilter} formatOption={(v) => `L${v}`} />
            <MultiFilterDropdown label="Any width" options={filters.lotWidths} selected={filters.lotWidthFilter} onChange={filters.setLotWidthFilter} formatOption={(v) => `${v} wide`} />
            <MultiFilterDropdown label="Any depth" options={filters.lotDepths} selected={filters.lotDepthFilter} onChange={filters.setLotDepthFilter} formatOption={(v) => `${v} deep`} />
            <MultiFilterDropdown label="Any pack" options={filters.assetPacks} selected={filters.assetPackFilter} onChange={filters.setAssetPackFilter} />
            <TextButton onSelect={filters.clearFilters}>Clear</TextButton>
        </div>
    </div>
);
