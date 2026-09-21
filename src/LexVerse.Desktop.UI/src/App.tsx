import { FormEvent, useEffect, useMemo, useState } from "react";
import { DesktopBridge } from "./bridge";
import type { DesktopSnapshot } from "./types";
import "./styles.css";

const languages = [
  ["auto", "Auto detect"],
  ["en", "English"],
  ["ja", "Japanese"],
  ["ko", "Korean"],
  ["zh", "Chinese"],
  ["vi", "Vietnamese"]
] as const;

export function App() {
  const bridge = useMemo(() => new DesktopBridge(), []);
  const [snapshot, setSnapshot] = useState<DesktopSnapshot>();
  const [quickText, setQuickText] = useState("");
  const [sourceLanguage, setSourceLanguage] = useState("auto");
  const [targetLanguage, setTargetLanguage] = useState("vi");
  const [selectedOffer, setSelectedOffer] = useState("");
  const [route, setRoute] = useState<"workspace" | "settings">("workspace");
  const [busy, setBusy] = useState<string>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    let active = true;
    bridge.command<DesktopSnapshot>("app.getSnapshot").then((next) => {
      if (!active) return;
      setSnapshot(next);
      setSourceLanguage(next.sourceLanguage);
      setTargetLanguage(next.targetLanguage);
      setSelectedOffer(next.commerce.offers[0]?.id ?? "");
    }).catch(() => setError("The native workspace is unavailable."));

    const unsubscribe = bridge.subscribe((event) => {
      if (event.type !== "app.snapshot") return;
      const next = event.payload as DesktopSnapshot;
      setSnapshot(next);
      setSelectedOffer((current) => current || next.commerce.offers[0]?.id || "");
    });

    return () => {
      active = false;
      unsubscribe();
      bridge.dispose();
    };
  }, [bridge]);

  async function run(label: string, action: () => Promise<unknown>) {
    setBusy(label);
    setError(undefined);
    try {
      await action();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "operation-failed");
    } finally {
      setBusy(undefined);
    }
  }

  function translate(event: FormEvent) {
    event.preventDefault();
    if (!quickText.trim()) {
      setError("Enter text before translating.");
      return;
    }
    void run("Translating", () => bridge.command("translation.translateQuickText", {
      text: quickText,
      sourceLanguage,
      targetLanguage
    }));
  }

  const realtimeRunning = snapshot?.realtime.status === "running" || snapshot?.realtime.status === "starting";

  return (
    <main className="shell">
      <header className="masthead">
        <div className="brand-mark" aria-hidden="true">LV</div>
        <div className="brand-copy">
          <p className="eyebrow">Realtime language workspace</p>
          <h1>LexVerse</h1>
        </div>
        <nav className="top-nav" aria-label="Workspace navigation">
          <button className={route === "workspace" ? "nav-active" : ""} onClick={() => setRoute("workspace")}>Workspace</button>
          <button className={route === "settings" ? "nav-active" : ""} onClick={() => setRoute("settings")}>Settings</button>
        </nav>
        <div className="hotkey"><span />{snapshot?.hotkey ?? "F6 starting"}</div>
      </header>

      {route === "workspace" ? (
        <section className="workspace">
          <aside className="rail" aria-label="Translation actions">
            <p className="section-number">01 / ACTIONS</p>
            <h2>Read without breaking your rhythm.</h2>
            <p className="lede">Translate selected text or pin a living translation over a document region.</p>
            <button
              className="action action-primary"
              disabled={!snapshot?.privacy.allowRemoteTextProcessing || Boolean(busy)}
              onClick={() => void run("Selecting region", () => bridge.command("realtime.selectRegion"))}
            >
              <span>Document region</span><b>⌗</b>
            </button>
            <button
              className="action"
              disabled={!realtimeRunning || Boolean(busy)}
              onClick={() => void run("Stopping", () => bridge.command("realtime.stop"))}
            >
              <span>Stop translation</span><b>■</b>
            </button>
            <div className="session-card">
              <span className={`signal ${realtimeRunning ? "signal-live" : ""}`} />
              <div><small>SESSION</small><strong>{snapshot?.realtime.status ?? "loading"}</strong></div>
              {snapshot?.realtime.lastFrameMilliseconds != null && <em>{snapshot.realtime.lastFrameMilliseconds.toFixed(0)} ms</em>}
            </div>
          </aside>

          <section className="reader-column">
            <div className="chapter-line"><span>02</span><i /><span>QUICK TRANSLATION</span></div>
            <form className="translation-sheet" onSubmit={translate}>
              <div className="language-row">
                <label>From
                  <select value={sourceLanguage} onChange={(event) => setSourceLanguage(event.target.value)}>
                    {languages.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                  </select>
                </label>
                <span className="language-arrow" aria-hidden="true">→</span>
                <label>To
                  <select value={targetLanguage} onChange={(event) => setTargetLanguage(event.target.value)}>
                    {languages.filter(([value]) => value !== "auto").map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                  </select>
                </label>
              </div>
              <label className="editor-label" htmlFor="quick-text">Text to translate</label>
              <textarea
                id="quick-text"
                value={quickText}
                onChange={(event) => setQuickText(event.target.value)}
                placeholder="Paste a passage, a line of dialogue, or a difficult phrase…"
                maxLength={32_000}
              />
              {snapshot?.quickTranslation && (
                <output className="translation-result" aria-live="polite">
                  <span>Translation</span>
                  <p>{snapshot.quickTranslation}</p>
                </output>
              )}
              <div className="sheet-footer">
                <span>{quickText.length.toLocaleString()} characters</span>
                <button className="translate-button" disabled={Boolean(busy) || !snapshot?.privacy.allowRemoteTextProcessing}>
                  {busy === "Translating" ? "Translating…" : "Translate passage"}
                </button>
              </div>
            </form>
            <div className="status-line" role="status" aria-live="polite">
              <span>STATUS</span>
              <p>{error ? `Could not complete: ${error}` : busy ?? snapshot?.status ?? "Loading native modules…"}</p>
            </div>
          </section>

          <aside className="inspector">
            <div className="chapter-line"><span>03</span><i /><span>ACCOUNT &amp; PRIVACY</span></div>
            <article className="folio">
              <p className="folio-kicker">CURRENT EDITION</p>
              <h3>{snapshot?.commerce.configured ? snapshot.commerce.status : "Free reader"}</h3>
              <p>{snapshot?.commerce.displayName ?? "No account required for Popup and Document translation."}</p>
              {snapshot?.commerce.configured && (
                <>
                  <select aria-label="Available plans" value={selectedOffer} onChange={(event) => setSelectedOffer(event.target.value)}>
                    {snapshot.commerce.offers.map((offer) => <option key={offer.id} value={offer.id}>{offer.label}</option>)}
                  </select>
                  <div className="folio-actions">
                    <button onClick={() => void run("Signing in", () => bridge.command("account.signIn"))}>Sign in</button>
                    <button disabled={!selectedOffer} onClick={() => void run("Opening checkout", () => bridge.command("commerce.checkout", { offerId: selectedOffer }))}>Choose plan</button>
                  </div>
                </>
              )}
            </article>
            <article className="privacy-card">
              <div><span>External processing</span><strong>{snapshot?.privacy.allowRemoteTextProcessing ? "Allowed" : "Off"}</strong></div>
              <div><span>Local diagnostics</span><strong>{snapshot?.privacy.allowLocalDiagnostics ? "On" : "Off"}</strong></div>
              <button onClick={() => setRoute("settings")}>Review privacy settings</button>
            </article>
            <p className="provider-note"><span /> Provider path: local OCR → secure translation API</p>
          </aside>
        </section>
      ) : (
        <Settings snapshot={snapshot} bridge={bridge} run={run} />
      )}
    </main>
  );
}

function Settings({ snapshot, bridge, run }: {
  snapshot?: DesktopSnapshot;
  bridge: DesktopBridge;
  run: (label: string, action: () => Promise<unknown>) => Promise<void>;
}) {
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
