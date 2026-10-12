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

const initialPreview: DesktopSnapshot = {
  moduleCount: 2,
  status: "Preview mode — waiting for the native host.",
  hotkey: "F6 preview",
  quickTranslation: "",
  sourceLanguage: "auto",
  targetLanguage: "vi",
  privacy: { allowRemoteTextProcessing: false, allowLocalDiagnostics: true },
  realtime: {
    status: "stopped",
    canStartRegion: false,
    canStop: false,
    performanceBudgetExceeded: false
  },
  commerce: { configured: false, status: "not connected", offers: [] }
};

export class DesktopBridge {
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
    this.transport?.addEventListener("message", this.onMessage);
  }

  dispose() {
    this.transport?.removeEventListener("message", this.onMessage);
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
      if (type === "app.getSnapshot") return initialPreview as T;
      throw new Error("native-host-unavailable");
    }

    const id = crypto.randomUUID();
    const command: BridgeCommand = { version: 1, id, type, payload };
    return new Promise<T>((resolve, reject) => {
      const timer = window.setTimeout(() => {
        this.pending.delete(id);
        reject(new Error("bridge-timeout"));
      }, 30_000);
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject, timer });
      this.transport!.postMessage(command);
    });
  }
}
