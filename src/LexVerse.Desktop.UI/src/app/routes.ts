import type { ComponentType } from "react";
import { WorkspacePage } from "../modules/workspace/WorkspacePage";
import { SettingsPage } from "../modules/settings/SettingsPage";
import type { AppRoute, RouteProps } from "./route-types";

export const routes = {
  workspace: { label: "Workspace", component: WorkspacePage },
  settings: { label: "Settings", component: SettingsPage }
} satisfies Record<AppRoute, { label: string; component: ComponentType<RouteProps> }>;

export const routeIds = Object.keys(routes) as AppRoute[];
