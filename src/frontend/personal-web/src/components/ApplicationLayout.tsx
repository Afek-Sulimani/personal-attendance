import LoadingIndicator from "./LoadingIndicator";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import type { AuthenticatedUser } from "../features/auth/authApi";
import { getPendingAttendanceCorrections } from "../features/attendance/correctionApi";

type ApplicationLayoutProps = {
  user: AuthenticatedUser;
  children: ReactNode;
  isSigningOut: boolean;
  onSignOut: () => void;
};

const navigationItems = [
  { label: "My attendance", path: "/attendance", roles: null },
  { label: "My monthly attendance", path: "/reports/monthly", roles: null },
  { label: "Manage people", path: "/admin/people", roles: ["Admin", "HR", "PayrollRepresentative"] },
  { label: "Employee pay details", path: "/admin/employee-profiles", roles: ["Admin", "HR", "PayrollRepresentative"] },
  { label: "Company settings", path: "/payroll/company-settings", roles: ["Admin", "PayrollRepresentative"] },
  { label: "Workday setup", path: "/payroll/work-days", roles: ["Admin", "PayrollRepresentative"] },
  { label: "Attendance administration", path: "/admin/attendance", roles: ["Admin"] },
  { label: "Review corrections", path: "/team/attendance-corrections", roles: ["Admin", "TeamLead", "HR", "PayrollRepresentative"] },
  { label: "Team monthly attendance", path: "/team/monthly-reports", roles: ["Admin", "TeamLead", "HR", "PayrollRepresentative"] },
] as const;

function formatRole(role: string): string {
  switch (role) {
    case "Admin": return "Admin";
    case "PayrollRepresentative": return "Payroll";
    case "TeamLead": return "Team lead";
    case "HR": return "HR";
    default: return role;
  }
}

export default function ApplicationLayout({
  user,
  children,
  isSigningOut,
  onSignOut,
}: ApplicationLayoutProps) {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [attentionCount, setAttentionCount] = useState(0);
  const canReviewCorrections = user.roles.some(role =>
    ["Admin", "TeamLead", "HR", "PayrollRepresentative"].includes(role));

  useEffect(() => {
    if (!canReviewCorrections) return;
    let controller = new AbortController();
    const refresh = () => {
      controller.abort();
      controller = new AbortController();
      const activeController = controller;
      getPendingAttendanceCorrections(activeController.signal)
        .then(requests => setAttentionCount(requests.filter(request => request.isAttentionRequired).length))
        .catch(() => { if (!activeController.signal.aborted) setAttentionCount(0); });
    };
    refresh();
    window.addEventListener("attendance-corrections-updated", refresh);
    return () => {
      controller.abort();
      window.removeEventListener("attendance-corrections-updated", refresh);
    };
  }, [canReviewCorrections]);
  const availableItems = navigationItems.filter(item =>
    item.roles === null || item.roles.some(role => user.roles.includes(role)),
  );

  return (
    <div className="application-shell">
      <button
        className="mobile-menu-toggle"
        type="button"
        aria-expanded={isMobileMenuOpen}
        aria-controls="application-sidebar"
        onClick={() => setIsMobileMenuOpen(open => !open)}
      >
        <span aria-hidden="true">☰</span>
        <span>{isMobileMenuOpen ? "Close menu" : "Open menu"}</span>
      </button>

      <aside
        id="application-sidebar"
        className={`application-sidebar${isMobileMenuOpen ? " is-open" : ""}`}
      >
        <NavLink className="application-brand" to="/attendance" onClick={() => setIsMobileMenuOpen(false)}>
          <span className="application-brand-name">PERSONAL</span>
          <span className="application-brand-divider" aria-hidden="true">/</span>
          <span className="application-brand-product">ATTENDANCE</span>
        </NavLink>

        <nav className="application-navigation" aria-label="Main navigation">
          {availableItems.map(item => (
            <NavLink
              key={item.path}
              className={({ isActive }) => `application-nav-link${isActive ? " is-active" : ""}`}
              to={item.path}
              onClick={() => setIsMobileMenuOpen(false)}
            >
              <span className="application-nav-mark" aria-hidden="true" />
              {item.label}
              {item.path === "/team/attendance-corrections" && attentionCount > 0 && (
                <span className="application-nav-attention" aria-label={`${attentionCount} corrections need attention`} />
              )}
            </NavLink>
          ))}
        </nav>

        <div className="application-user-panel">
          <NavLink
            className={({ isActive }) => `application-user-avatar${isActive ? " is-active" : ""}`}
            to="/profile"
            aria-label={`Open profile for ${user.displayName}`}
            title="My profile"
          >
            {user.displayName.trim().slice(0, 1).toLocaleUpperCase() || "U"}
          </NavLink>
          <div className="application-user-details">
            <strong>{user.displayName}</strong>
            <span>Company {user.companyCode}{user.employeeNumber ? ` · Employee ${user.employeeNumber}` : ""}</span>
          </div>
          <div className="application-user-actions">
            <div className="application-role-list" aria-label="Your roles">
              {user.roles.map(role => (
                <span className="application-role" key={role}>{formatRole(role)}</span>
              ))}
            </div>
            <button aria-busy={isSigningOut}
              className="application-sign-out"
              type="button"
              onClick={onSignOut}
              disabled={isSigningOut}
            >
              {isSigningOut ? <LoadingIndicator label="Signing out…" /> : "Sign out"}
            </button>
          </div>
        </div>
      </aside>

      {isMobileMenuOpen && (
        <button
          className="application-backdrop"
          type="button"
          aria-label="Close navigation menu"
          onClick={() => setIsMobileMenuOpen(false)}
        />
      )}

      <main className="application-main">{children}</main>
    </div>
  );
}
