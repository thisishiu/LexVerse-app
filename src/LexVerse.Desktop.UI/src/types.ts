export type CommandType =
  | "app.getSnapshot"
  | "app.uiReady"
  | "app.introCompleted"
  | "translation.translateQuickText"
  | "translation.triggerSelectedText"
  | "realtime.selectRegion"
  | "realtime.start"
  | "realtime.startFullScreen"
  | "realtime.stop"
  | "realtime.resetOverlay"
  | "settings.update"
  | "privacy.updateConsent"
  | "account.signIn"
  | "account.reopenSignIn"
  | "account.copySignInLink"
  | "account.cancelSignIn"
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

export interface DesktopShortcuts {
  popup: string;
  region: string;
  fullScreen: string;
  stop: string;
  reset: string;
}

export interface DesktopSettings {
  appLanguage: "en" | "vi";
  sourceLanguage: string;
  targetLanguage: string;
  ocrMode: "fullscreen" | "subtitle" | "game-dialogue" | "document" | "comic";
  ignoredTerms: string;
  preserveAcronymsAndTechnicalTerms: boolean;
  closePopupOnOutsideClick: boolean;
  allowPopupDragging: boolean;
  showOcrDebugBoxes: boolean;
  showPopupBeforeTranslationFinishes: boolean;
  scanIntervalMs: number;
  overlayOpacityPercent: number;
  overlayFontScalePercent: number;
  useDarkOverlayBlocks: boolean;
  translationInstruction: string;
  shortcuts: DesktopShortcuts;
}

export interface DesktopSnapshot {
  moduleCount: number;
  status: string;
  hotkey: string;
  quickTranslation: string;
  sourceLanguage: string;
  targetLanguage: string;
  settings: DesktopSettings;
  privacy: {
    allowRemoteTextProcessing: boolean;
    allowLocalDiagnostics: boolean;
  };
  realtime: {
    status: string;
    canStartRegion: boolean;
    canStartFullScreen: boolean;
    canStop: boolean;
    lastFrameMilliseconds?: number;
    performanceBudgetExceeded: boolean;
  };
  commerce: {
    configured: boolean;
    status: string;
    displayName?: string;
    authentication?: { status: string; error?: string };
    offers: OfferSnapshot[];
  };
}
