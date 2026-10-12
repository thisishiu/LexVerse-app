export type CommandType =
  | "app.getSnapshot"
  | "app.uiReady"
  | "translation.translateQuickText"
  | "realtime.selectRegion"
  | "realtime.start"
  | "realtime.stop"
  | "settings.update"
  | "privacy.updateConsent"
  | "account.signIn"
  | "account.signOut"
  | "commerce.checkout"
  | "commerce.restore"
  | "external.openHelp";

export interface BridgeCommand<T = unknown> {
  version: 1;
  id: string;
  type: CommandType;
  payload: T;
}

export interface BridgeResult<T = unknown> {
  version: 1;
  id: string;
  ok: boolean;
  payload?: T;
  errorCode?: string;
}

export interface HostEvent<T = unknown> {
  version: 1;
  type: string;
  payload: T;
}

export interface OfferSnapshot {
  id: string;
  label: string;
}

export interface DesktopSnapshot {
  moduleCount: number;
  status: string;
  hotkey: string;
  quickTranslation: string;
  sourceLanguage: string;
  targetLanguage: string;
  privacy: {
    allowRemoteTextProcessing: boolean;
    allowLocalDiagnostics: boolean;
  };
  realtime: {
    status: string;
    canStartRegion: boolean;
    canStop: boolean;
    lastFrameMilliseconds?: number;
    performanceBudgetExceeded: boolean;
  };
  commerce: {
    configured: boolean;
    status: string;
    displayName?: string;
    offers: OfferSnapshot[];
  };
}
