import { useMemo, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import type { InvitationManager } from "./employeeInvitationsAdminApi";
import { matchesEmployeeSearch } from "../../lib/matchesEmployeeSearch";

type ManagerComboboxProps = {
  managers: InvitationManager[];
  selectedId: string;
  onChange: (membershipId: string) => void;
  disabled: boolean;
};

function managerLabel(manager: InvitationManager): string {
  return manager.employeeNumber
    ? `${manager.displayName} · ${manager.employeeNumber}`
    : manager.displayName;
}

export default function ManagerCombobox({ managers, selectedId, onChange, disabled }: ManagerComboboxProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const selectedManager = managers.find(manager => manager.membershipId === selectedId);
  const filteredManagers = useMemo(() => managers.filter(manager =>
    matchesEmployeeSearch(manager.displayName, manager.employeeNumber, search)
  ), [managers, search]);

  function closeList() {
    setIsOpen(false);
    setSearch("");
    setActiveIndex(0);
  }

  function chooseManager(manager: InvitationManager) {
    onChange(manager.membershipId);
    closeList();
    inputRef.current?.blur();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      closeList();
    } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (!isOpen) {
        setIsOpen(true);
        setSearch("");
        setActiveIndex(0);
      } else if (filteredManagers.length > 0) {
        setActiveIndex(index => (index + (event.key === "ArrowDown" ? 1 : -1) + filteredManagers.length) % filteredManagers.length);
      }
    } else if (event.key === "Enter" && isOpen && filteredManagers[activeIndex]) {
      event.preventDefault();
      chooseManager(filteredManagers[activeIndex]);
    }
  }

  return (
    <div className="form-field manager-combobox">
      <label htmlFor="direct-manager">Direct manager</label>
      <input
        ref={inputRef}
        id="direct-manager"
        type="text"
        role="combobox"
        aria-autocomplete="list"
        aria-required="true"
        aria-expanded={isOpen}
        aria-controls="direct-manager-options"
        aria-activedescendant={isOpen && filteredManagers[activeIndex]
          ? `direct-manager-option-${filteredManagers[activeIndex].membershipId}`
          : undefined}
        autoComplete="off"
        disabled={disabled}
        placeholder="Search by name or employee number"
        value={isOpen ? search : selectedManager ? managerLabel(selectedManager) : ""}
        onFocus={() => { setSearch(""); setActiveIndex(0); setIsOpen(true); }}
        onBlur={closeList}
        onChange={event => { setSearch(event.target.value); setActiveIndex(0); setIsOpen(true); }}
        onKeyDown={handleKeyDown}
      />
      {isOpen && (
        <div id="direct-manager-options" className="manager-combobox-options" role="listbox" aria-label="Direct managers">
          {filteredManagers.length === 0 ? (
            <p className="manager-combobox-empty">No managers match this search.</p>
          ) : filteredManagers.map((manager, index) => (
            <div
              id={`direct-manager-option-${manager.membershipId}`}
              className={`manager-combobox-option${index === activeIndex ? " is-active" : ""}`}
              role="option"
              aria-selected={manager.membershipId === selectedId}
              key={manager.membershipId}
              onPointerDown={event => { event.preventDefault(); chooseManager(manager); }}
            >
              {managerLabel(manager)}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
