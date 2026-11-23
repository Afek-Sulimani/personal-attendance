import { useState } from "react";

export default function CopyableValue({
  label,
  value,
  displayText,
}: {
  label: string;
  value: string;
  displayText: string;
}) {
  const [copyStatus, setCopyStatus] = useState("");

  async function copyValue() {
    try {
      await navigator.clipboard.writeText(value);
      setCopyStatus(`${label} copied.`);
    } catch {
      setCopyStatus("Clipboard access is unavailable. Allow clipboard access in the browser and try again.");
    }
  }

  return (
    <div className="copyable-value">
      <div className="copyable-value-row">
        <a className="copyable-link" href={value}>
          {displayText}
        </a>
        <button
          className="secondary-button"
          type="button"
          aria-label={`Copy ${label}`}
          onClick={() => void copyValue()}
        >
          Copy
        </button>
      </div>
      {copyStatus && <span className="copy-status" role="status">{copyStatus}</span>}
    </div>
  );
}
