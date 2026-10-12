import type { DesktopApp } from "./useDesktopApp";

export type AppRoute = "workspace" | "settings";

export interface RouteProps {
  app: DesktopApp;
  navigate: (route: AppRoute) => void;
}
