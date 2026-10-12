import type { FormEvent } from "react";
import type { RouteProps } from "../../app/route-types";

const languages = [
  ["auto", "Auto detect"],
  ["en", "English"],
  ["ja", "Japanese"],
  ["ko", "Korean"],
  ["zh", "Chinese"],
  ["vi", "Vietnamese"]
] as const;

export function WorkspacePage({ app, navigate }: RouteProps) {
  const { bridge, snapshot, quickText, setQuickText, sourceLanguage, setSourceLanguage, targetLanguage, setTargetLanguage, selectedOffer, setSelectedOffer, busy, error, setError, run } = app;

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
          <h3>{snapshot?.commerce.configured ? snapshot.commerce.status : "Connect your account"}</h3>
          <p>{snapshot?.commerce.displayName ?? "Sign in with a verified email to translate, including on the Free plan."}</p>
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
          <button onClick={() => navigate("settings")}>Review privacy settings</button>
        </article>
        <p className="provider-note"><span /> Provider path: local OCR → secure translation API</p>
      </aside>
    </section>
  );
}
