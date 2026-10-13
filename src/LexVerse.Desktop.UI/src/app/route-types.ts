import type { DesktopApp } from "./useDesktopApp";

export type AppRoute = "workspace" | "account" | "settings";

export interface RouteProps {
  app: DesktopApp;
  navigate: (route: AppRoute) => void;
}
