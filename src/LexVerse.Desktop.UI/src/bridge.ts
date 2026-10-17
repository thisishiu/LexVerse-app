import type { BridgeCommand, BridgeResult, CommandType, DesktopSnapshot, HostEvent } from "./types";

type MessageHandler = (event: { data: unknown }) => void;

interface WebViewTransport {
  postMessage(message: unknown): void;
  addEventListener(type: "message", handler: MessageHandler): void;
  removeEventListener(type: "message", handler: MessageHandler): void;
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewTransport };
  }
}

const basePreview: DesktopSnapshot = {
  moduleCount: 2,
  status: "Preview mode - waiting for the native host.",
  hotkey: "F6 preview",
  quickTranslation: "",
  sourceLanguage: "auto",
  targetLanguage: "vi",
  settings: {
    appLanguage: "en",
    sourceLanguage: "auto",
    targetLanguage: "vi",
    ocrMode: "document",
    ignoredTerms: "HP, MP, EXP",
    preserveAcronymsAndTechnicalTerms: true,
    closePopupOnOutsideClick: true,
    allowPopupDragging: true,
    showOcrDebugBoxes: false,
    showPopupBeforeTranslationFinishes: false,
    scanIntervalMs: 500,
    overlayOpacityPercent: 85,
    overlayFontScalePercent: 100,
    useDarkOverlayBlocks: true,
    translationInstruction: "Translate naturally and clearly. Keep proper names, technical terms, HP/MP/EXP unchanged. Do not add explanations.",
    shortcuts: {
      popup: "F6",
      region: "Ctrl+Shift+R",
      fullScreen: "Ctrl+Shift+F",
      stop: "Escape",
      reset: "Ctrl+Shift+Backspace"
    }
  },
  privacy: { allowRemoteTextProcessing: false, allowLocalDiagnostics: true },
  realtime: {
    status: "stopped",
    canStartRegion: false,
    canStartFullScreen: false,
    canStop: false,
    performanceBudgetExceeded: false
  },
  commerce: { configured: false, status: "not connected", offers: [] }
};

function getPreviewSnapshot(): DesktopSnapshot {
  const mode = new URLSearchParams(window.location.search).get("preview");
  if (mode !== "ready" && mode !== "signed-in") return basePreview;

  return {
    ...basePreview,
    status: "Preview mode - desktop services ready.",
    hotkey: "F6 registered globally",
    privacy: { allowRemoteTextProcessing: true, allowLocalDiagnostics: true },
    realtime: {
      status: "stopped",
      canStartRegion: true,
      canStartFullScreen: true,
      canStop: false,
      lastFrameMilliseconds: 84,
      performanceBudgetExceeded: false
    },
    commerce: {
      configured: true,
      status: mode === "signed-in" ? "SIGNED IN" : "SIGNED OUT",
      displayName: mode === "signed-in" ? "Demo User" : undefined,
      offers: [
        { id: "pro-monthly", label: "LexVerse Pro - monthly" },
        { id: "pro-yearly", label: "LexVerse Pro - yearly" }
      ]
    }
  };
}

export class DesktopBridge {
  private connected = false;
  private readonly pending = new Map<string, {
    resolve: (value: unknown) => void;
    reject: (reason: Error) => void;
    timer: number;
  }>();

  private readonly listeners = new Set<(event: HostEvent) => void>();
  private readonly transport = window.chrome?.webview;

  private readonly onMessage = (event: { data: unknown }) => {
    if (!event.data || typeof event.data !== "object") return;
    const message = event.data as Partial<BridgeResult> & Partial<HostEvent>;
    if (message.version !== 1) return;

    if (typeof message.id === "string" && this.pending.has(message.id)) {
      const pending = this.pending.get(message.id)!;
      window.clearTimeout(pending.timer);
      this.pending.delete(message.id);
      if (message.ok) pending.resolve(message.payload);
      else pending.reject(new Error(message.errorCode ?? "bridge-failed"));
      return;
    }

    if (typeof message.type === "string") {
      this.listeners.forEach((listener) => listener(message as HostEvent));
    }
  };

  constructor() {
    this.connect();
  }

  connect() {
    if (this.connected || !this.transport) return;
    this.transport.addEventListener("message", this.onMessage);
    this.connected = true;
  }

  dispose() {
    if (this.connected) {
      this.transport?.removeEventListener("message", this.onMessage);
      this.connected = false;
    }
    for (const pending of this.pending.values()) {
      window.clearTimeout(pending.timer);
      pending.reject(new Error("bridge-disposed"));
    }
    this.pending.clear();
    this.listeners.clear();
  }

  subscribe(listener: (event: HostEvent) => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  async command<T>(type: CommandType, payload: unknown = {}): Promise<T> {
    if (!this.transport) {
      if (type === "app.getSnapshot") return getPreviewSnapshot() as T;
      throw new Error("native-host-unavailable");
    }

    const id = crypto.randomUUID();
    const command: BridgeCommand = { version: 1, id, type, payload };
    return new Promise<T>((resolve, reject) => {
      const timer = window.setTimeout(() => {
        this.pending.delete(id);
        reject(new Error("bridge-timeout"));
      // Browser sign-in allows three minutes for user interaction, followed by
      // token exchange, account/catalog requests, and feature access refresh.
      }, 30_000);
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject, timer });
      this.transport!.postMessage(command);
    });
  }
}
