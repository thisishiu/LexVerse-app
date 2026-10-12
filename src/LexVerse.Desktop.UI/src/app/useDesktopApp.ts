import { useEffect, useMemo, useState } from "react";
import { DesktopBridge } from "../bridge";
import type { DesktopSnapshot } from "../types";

export function useDesktopApp() {
  const bridge = useMemo(() => new DesktopBridge(), []);
  const [snapshot, setSnapshot] = useState<DesktopSnapshot>();
  const [quickText, setQuickText] = useState("");
  const [sourceLanguage, setSourceLanguage] = useState("auto");
  const [targetLanguage, setTargetLanguage] = useState("vi");
  const [selectedOffer, setSelectedOffer] = useState("");
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

  return { bridge, snapshot, quickText, setQuickText, sourceLanguage, setSourceLanguage, targetLanguage, setTargetLanguage, selectedOffer, setSelectedOffer, busy, error, setError, run };
}

export type DesktopApp = ReturnType<typeof useDesktopApp>;
