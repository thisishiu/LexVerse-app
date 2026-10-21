import { useEffect, useMemo, useRef, useState } from "react";
import { DesktopBridge } from "../bridge";
import type { DesktopSnapshot, PrivacySnapshot } from "../types";

export function useDesktopApp() {
  const bridge = useMemo(() => new DesktopBridge(), []);
  const [snapshot, setSnapshot] = useState<DesktopSnapshot>();
  const [quickText, setQuickText] = useState("");
  const [sourceLanguage, setSourceLanguage] = useState("auto");
  const [targetLanguage, setTargetLanguage] = useState("vi");
  const [selectedOffer, setSelectedOffer] = useState("");
  const [busy, setBusy] = useState<string>();
  const [error, setError] = useState<string>();
  const [privacySaving, setPrivacySaving] = useState(false);
  const [privacyError, setPrivacyError] = useState<string>();
  const privacyPending = useRef(false);
  const settingsLanguagePair = useRef("");

  useEffect(() => {
    let active = true;
    bridge.connect();
    bridge.command<DesktopSnapshot>("app.getSnapshot").then((next) => {
      if (!active) return;
      setSnapshot(current => current && (current.privacy.revision ?? 0) > (next.privacy.revision ?? 0)
        ? { ...next, privacy: current.privacy } : next);
      setSourceLanguage(next.sourceLanguage);
      setTargetLanguage(next.targetLanguage);
      settingsLanguagePair.current = `${next.settings.sourceLanguage}:${next.settings.targetLanguage}`;
      setSelectedOffer(next.commerce.offers[0]?.id ?? "");
    }).catch(() => {
      if (active) setError("The native workspace is unavailable.");
    });

    const unsubscribe = bridge.subscribe((event) => {
      if (event.type !== "app.snapshot") return;
      const next = event.payload as DesktopSnapshot;
      setSnapshot(current => current && (current.privacy.revision ?? 0) > (next.privacy.revision ?? 0)
        ? { ...next, privacy: current.privacy } : next);
      const nextPair = `${next.settings.sourceLanguage}:${next.settings.targetLanguage}`;
      if (nextPair !== settingsLanguagePair.current) {
        settingsLanguagePair.current = nextPair;
        setSourceLanguage(next.settings.sourceLanguage);
        setTargetLanguage(next.settings.targetLanguage);
      }
      setSelectedOffer((current) => next.commerce.offers.some((offer) => offer.id === current)
        ? current
        : next.commerce.offers[0]?.id ?? "");
    });

    return () => {
      active = false;
      unsubscribe();
      bridge.dispose();
    };
  }, [bridge]);

  const hasSnapshot = !!snapshot;
  useEffect(() => {
    if (!hasSnapshot || !window.chrome?.webview) return;
    void bridge.command("app.uiReady").catch(() => setError("The workspace could not become ready."));
  }, [bridge, hasSnapshot]);

  async function run(label: string, action: () => Promise<unknown>) {
    setBusy(label);
    setError(undefined);
    try {
      await action();
    } catch (reason) {
      const code = reason instanceof Error ? reason.message : "operation-failed";
      if (code !== "operation-cancelled") setError(code);
    } finally {
      setBusy(undefined);
    }
  }

  async function updatePrivacy(allowRemoteTextProcessing: boolean, allowLocalDiagnostics: boolean) {
    if (privacyPending.current) return;
    privacyPending.current = true;
    setPrivacySaving(true);
    setPrivacyError(undefined);
    try {
      const privacy = await bridge.command<PrivacySnapshot>("privacy.updateConsent", {
        allowRemoteTextProcessing, allowLocalDiagnostics
      });
      setSnapshot(current => current && (current.privacy.revision ?? 0) <= (privacy.revision ?? 0)
        ? { ...current, privacy } : current);
    } catch (reason) {
      const code = reason instanceof Error ? reason.message : "operation-failed";
      if (code !== "consent-declined" && code !== "operation-cancelled") setPrivacyError(code);
    } finally {
      privacyPending.current = false;
      setPrivacySaving(false);
    }
  }

  return { bridge, snapshot, quickText, setQuickText, sourceLanguage, setSourceLanguage, targetLanguage, setTargetLanguage, selectedOffer, setSelectedOffer, busy, error, setError, run, privacySaving, privacyError, updatePrivacy };
}

export type DesktopApp = ReturnType<typeof useDesktopApp>;
