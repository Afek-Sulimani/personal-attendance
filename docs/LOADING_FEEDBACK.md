# Loading feedback

`LoadingIndicator` is the shared React component for initial page loads and pending actions. It displays a small CSS spinner beside an explicit label. It inherits the current text color to match authentication and application buttons. The spinner is decorative (`aria-hidden`); existing status regions announce page loading, while buttons retain a readable action label and expose `aria-busy`.

Usage covers session/invitation checks, sign-in and activation, company/member creation, attendance punches and corrections, correction review, settings/workdays/compensation/password saves, audited Admin edits, sign-out, report loads, and diagnostic time requests. Existing pending state disables action buttons until completion. Success appears only after the request succeeds; errors replace loading feedback and restore the controls. Initial-load errors show the error message without an endless spinner.

Calendar refresh uses a small inline status and retains existing content. Automatic refresh never replaces the entire attendance screen with a loading page. Loading indicators do not generate time values or additional API calls. The spinner animation is disabled under `prefers-reduced-motion: reduce` while its label remains visible.

Playwright `cross-08-loading-feedback.spec.ts` holds a sign-in response to verify the spinner, busy/disabled button, single submission, and recovery after an error. A second case verifies static feedback with reduced motion and spinner removal after session checking. The full role suite checks compatibility with existing flows. Screenshots are attached locally to the Playwright report.
