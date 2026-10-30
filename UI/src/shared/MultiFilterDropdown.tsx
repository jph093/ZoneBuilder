import * as cs2ui from "cs2/ui";
import { Dropdown, DropdownToggle } from "cs2/ui";
import { ReactNode } from "react";
import classNames from "classnames";
import styles from "shared/FilterDropdown.module.scss";
import { ANY, dropdownTheme } from "shared/FilterDropdown";

// Same broken-named-export workaround as FilterDropdown.tsx - see its
// own comment for why this cast exists.
const DropdownItem = cs2ui.DropdownItem as unknown as (props: {
    value: string;
    selected?: boolean;
    onChange?: (value: string) => void;
    onToggleSelected?: (value: string) => void;
    closeOnSelect?: boolean;
    children?: ReactNode;
    className?: string;
}) => JSX.Element;

export interface MultiFilterDropdownProps {
    label: string;
    options: string[];
    selected: string[];
    onChange: (next: string[]) => void;
    formatOption?: (value: string) => string;
}

// FilterDropdown's multi-select sibling (2026-10-26, user feedback: the
// Assets tab's filters should let you pick several values at once, e.g.
// Low AND Medium density in one go, not force five separate browsing
// passes). Deliberately a new component rather than extending
// FilterDropdown itself - ZonesTab.tsx uses that one for two genuinely
// single-select pickers (a zone's base type, attaching one collection at
// a time), and cs2/ui's own DropdownItem docs call it "mutually exclusive
// values" by design, so bending it to also do multi-select in place would
// risk exactly that kind of single-select caller breaking.
//
// Built on the same DropdownItem the single-select version uses, just
// pointed at a Set instead of one string - nothing enforces "mutually
// exclusive" inside the component itself, that's purely a convention of
// how FilterDropdown happens to use it; marking several items `selected`
// at once works fine. onChange fires for a currently-unselected item,
// onToggleSelected for a currently-selected one (re-clicking it to turn
// it back off) - see cs2/ui's own DropdownItemProps doc comments for why
// both are needed instead of just one.
//
// closeOnSelect={false} on every real option - the dropdown would
// otherwise hide itself after the very first pick (its default,
// single-select-oriented behavior), defeating the point of letting
// someone pick several values in one open session. The "any" row at the
// top leaves the default (closes) - picking it is a deliberate "I'm done
// narrowing, clear this filter" action, not another value to combine.
export const MultiFilterDropdown = ({ label, options, selected, onChange, formatOption }: MultiFilterDropdownProps) => {
    const format = formatOption ?? ((v: string) => v);
    const itemClassName = (isSelected: boolean) => classNames(styles.item, isSelected && styles.itemSelected);
    const isSelected = (option: string) => selected.indexOf(option) !== -1;

    // Plain array, not a Set - confirmed in-game (UAT, 2026-10-26):
    // picking a second value replaced the first instead of adding to it,
    // with no other explanation surviving a direct read of the game's own
    // compiled DropdownItem/Dropdown (Cities2_Data/Content/Game/UI/index.js)
    // - that component has no built-in exclusivity, it only ever reflects
    // whatever `selected` prop each item is given. The one remaining
    // suspect was `new Set(selected)` (copying from an iterable) silently
    // not working in Coherent's embedded JS runtime - this project has
    // already hit two other non-standard-JS-API surprises there (raw
    // `<select>` crashed the whole HUD, Web Crypto API never confirmed
    // present - see DECISIONS.md/generateGuid.ts). Rather than prove that
    // theory, array methods (indexOf/filter/spread) have no equivalent
    // risk and are simpler besides.
    const toggle = (option: string) => {
        onChange(isSelected(option) ? selected.filter((v) => v !== option) : [...selected, option]);
    };

    const toggleLabel =
        selected.length === 0 ? label : selected.length === 1 ? format(selected[0]) : `${selected.length} selected`;

    return (
        <Dropdown
            theme={dropdownTheme}
            content={
                <div className={styles.popup}>
                    <DropdownItem value={ANY} selected={selected.length === 0} onChange={() => onChange([])} className={itemClassName(selected.length === 0)}>
                        {label}
                    </DropdownItem>
                    {options.map((option) => (
                        <DropdownItem
                            key={option}
                            value={option}
                            selected={isSelected(option)}
                            onChange={() => toggle(option)}
                            onToggleSelected={() => toggle(option)}
                            closeOnSelect={false}
                            className={itemClassName(isSelected(option))}
                        >
                            {format(option)}
                        </DropdownItem>
                    ))}
                </div>
            }
        >
            <DropdownToggle className={styles.toggle}>{toggleLabel}</DropdownToggle>
        </Dropdown>
    );
};
