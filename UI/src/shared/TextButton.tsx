import { Button } from "cs2/ui";
import { ReactNode } from "react";
import classNames from "classnames";
import styles from "shared/TextButton.module.scss";

interface TextButtonProps {
    children: ReactNode;
    onSelect?: () => void;
    disabled?: boolean;
    selected?: boolean;
    tooltipLabel?: ReactNode;
    // Extra class for the rare call site that needs to stand out from
    // every other TextButton in the panel (e.g. a destructive confirm) -
    // appended, never replaces, .button/.selected below.
    className?: string;
}

// Flattened replacement for a plain `<Button>` - see base.scss's
// flatButtonChrome for why. One wrapper, not a className added at every
// call site, since this chrome is still being dialed in via in-game
// screenshots and is easier to adjust from one place while that's true.
export const TextButton = ({ children, onSelect, disabled, selected, tooltipLabel, className }: TextButtonProps) => (
    <Button
        variant="flat"
        selected={selected}
        disabled={disabled}
        tooltipLabel={tooltipLabel}
        onSelect={onSelect}
        className={classNames(styles.button, selected && styles.selected, className)}
    >
        {children}
    </Button>
);
