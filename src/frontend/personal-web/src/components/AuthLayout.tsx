import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import FingerprintDecoration from "./FingerprintDecoration";

export default function AuthLayout({
  children,
  scrollable = false,
}: {
  children: ReactNode;
  scrollable?: boolean;
}) {
  return (
    <main className={`auth-page${scrollable ? " auth-page-scrollable" : ""}`}>
      <section className="auth-card" aria-label="Personal Attendance account access">
        <div className="auth-mark" aria-hidden="true">
          <FingerprintDecoration />
        </div>
        <Link className="auth-brand" to="/login" aria-label="Personal Attendance home">
          <span>PERSONAL</span>
          <span className="auth-brand-divider" aria-hidden="true">/</span>
          <span className="auth-brand-product">ATTENDANCE</span>
        </Link>
        {children}
      </section>
      <p className="auth-footer">Personal Attendance</p>
    </main>
  );
}
