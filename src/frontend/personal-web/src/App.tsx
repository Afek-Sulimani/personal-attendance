import { Navigate, Route, Routes } from "react-router-dom";
import { ActivationFlowProvider } from "./features/invitations/ActivationFlowContext";
import ActivationPage from "./pages/ActivationPage";
import CompanyRegistrationPage from "./pages/CompanyRegistrationPage";
import AttendancePage from "./pages/AttendancePage";
import PayrollWorkDaysPage from "./pages/PayrollWorkDaysPage";
import AttendanceCorrectionReviewPage from "./pages/AttendanceCorrectionReviewPage";
import CompanyPayrollSettingsPage from "./pages/CompanyPayrollSettingsPage";
import EmployeeInvitationsPage from "./pages/EmployeeInvitationsPage";
import AdminAttendancePage from "./pages/AdminAttendancePage";
import LoginPage from "./pages/LoginPage";
import TimeProviderDiagnosticPage from "./pages/TimeProviderDiagnosticPage";
import MonthlyAttendanceReportPage from "./pages/MonthlyAttendanceReportPage";
import TeamMonthlyAttendancePage from "./pages/TeamMonthlyAttendancePage";
import EmployeeProfilePage from "./pages/EmployeeProfilePage";
import EmployeeCompensationPage from "./pages/EmployeeCompensationPage";
import AuthLayout from "./components/AuthLayout";
import { Link } from "react-router-dom";

function NotFoundPage() {
  return (
    <AuthLayout>
      <div className="auth-heading">
        <p className="auth-eyebrow">PAGE NOT FOUND</p>
        <h1>This page isn&apos;t here</h1>
        <p className="auth-description">Check the address or return to the sign-in page.</p>
      </div>
      <Link className="primary-button button-link" to="/login">Go to sign in</Link>
    </AuthLayout>
  );
}

export default function App() {
  return (
    <ActivationFlowProvider>
      <Routes>
        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/activate" element={<ActivationPage />} />
        <Route path="/register-company" element={<CompanyRegistrationPage />} />
        <Route path="/attendance" element={<AttendancePage />} />
        <Route path="/profile" element={<EmployeeProfilePage />} />
        <Route path="/reports/monthly" element={<MonthlyAttendanceReportPage />} />
        <Route path="/payroll/work-days" element={<PayrollWorkDaysPage />} />
        <Route path="/payroll/company-settings" element={<CompanyPayrollSettingsPage />} />
        <Route path="/admin/people" element={<EmployeeInvitationsPage />} />
        <Route path="/admin/employee-profiles" element={<EmployeeCompensationPage />} />
        <Route path="/admin/attendance" element={<AdminAttendancePage />} />
        <Route path="/team/attendance-corrections" element={<AttendanceCorrectionReviewPage />} />
        <Route path="/team/monthly-reports" element={<TeamMonthlyAttendancePage />} />
        <Route path="/dev/time-check" element={<TimeProviderDiagnosticPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </ActivationFlowProvider>
  );
}
