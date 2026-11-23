import CopyableValue from "../../components/CopyableValue";

export type CreatedInvitationDetails = {
  activationUrl: string;
  displayName: string;
  email: string;
  managerName: string;
  roleNames: string[];
};

type CreatedInvitationPanelProps = {
  invitation: CreatedInvitationDetails;
  onAddAnother: () => void;
};

export default function CreatedInvitationPanel({ invitation, onAddAnother }: CreatedInvitationPanelProps) {
  return (
    <section className="payroll-workday-card invitation-created-card" aria-labelledby="invitation-created-heading" role="status">
      <div>
        <p className="attendance-eyebrow">INVITATION READY</p>
        <h2 id="invitation-created-heading">Share the activation link with {invitation.displayName}</h2>
        <p className="attendance-page-description">The activation link expires 48 hours after creation.</p>
      </div>
      <dl className="created-invitation-details">
        <div><dt>Name</dt><dd>{invitation.displayName}</dd></div>
        <div><dt>Email</dt><dd>{invitation.email}</dd></div>
        <div><dt>Direct manager</dt><dd>{invitation.managerName}</dd></div>
        <div><dt>Roles</dt><dd>{invitation.roleNames.join(", ")}</dd></div>
      </dl>
      <CopyableValue
        label="Activation link"
        value={invitation.activationUrl}
        displayText="Temporary link for activating an employee account"
      />
      <button className="primary-attendance-button" type="button" onClick={onAddAnother}>
        Add another employee
      </button>
    </section>
  );
}
