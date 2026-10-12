import type { RouteProps } from "../../app/route-types";

export function SettingsPage({ app }: RouteProps) {
  const { snapshot, bridge, run } = app;
  return (
    <section className="settings-page">
      <div className="settings-intro">
        <p className="section-number">SETTINGS / PRIVACY</p>
        <h2>Your reading stays yours.</h2>
        <p>LexVerse keeps capture and OCR on this device. Only text that needs translation leaves the app after consent.</p>
      </div>
      <div className="settings-list">
        <label className="setting-row">
          <span><strong>External text processing</strong><small>Allow selected, typed, or OCR text to reach the configured translation provider.</small></span>
          <input
            type="checkbox"
            checked={snapshot?.privacy.allowRemoteTextProcessing ?? false}
            onChange={(event) => void run("Saving privacy", () => bridge.command("privacy.updateConsent", {
              allowRemoteTextProcessing: event.target.checked,
              allowLocalDiagnostics: snapshot?.privacy.allowLocalDiagnostics ?? true
            }))}
          />
        </label>
        <label className="setting-row">
          <span><strong>Local performance diagnostics</strong><small>Store bounded, content-free timing metrics on this device.</small></span>
          <input
            type="checkbox"
            checked={snapshot?.privacy.allowLocalDiagnostics ?? true}
            onChange={(event) => void run("Saving diagnostics", () => bridge.command("privacy.updateConsent", {
              allowRemoteTextProcessing: snapshot?.privacy.allowRemoteTextProcessing ?? false,
              allowLocalDiagnostics: event.target.checked
            }))}
          />
        </label>
      </div>
    </section>
  );
}
