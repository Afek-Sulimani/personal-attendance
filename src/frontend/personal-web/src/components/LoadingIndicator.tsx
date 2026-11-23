type LoadingIndicatorProps = { label: string };

/** Keep the label visible; the surrounding button/status owns announcements. */
export default function LoadingIndicator({ label }: LoadingIndicatorProps) {
  return (
    <span className="loading-indicator">
      <span className="loading-spinner" aria-hidden="true" />
      <span>{label}</span>
    </span>
  );
}
