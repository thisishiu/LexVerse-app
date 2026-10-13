import type { ComponentType } from "react";
import { Languages, Settings2, UserRound, type LucideIcon } from "lucide-react";
import { AccountPage } from "../modules/account/AccountPage";
import { WorkspacePage } from "../modules/workspace/WorkspacePage";
import { SettingsPage } from "../modules/settings/SettingsPage";
import type { AppRoute, RouteProps } from "./route-types";

export const routes = {
  workspace: {
    label: "Translate",
    title: "Translation workspace",
    description: "Quick text and live screen translation",
    icon: Languages,
    component: WorkspacePage
  },
  account: {
    label: "Account",
    title: "Account and access",
    description: "Identity, plans and entitlements",
    icon: UserRound,
    component: AccountPage
  },
  settings: {
    label: "Settings",
    title: "Application settings",
    description: "Privacy, defaults and diagnostics",
    icon: Settings2,
    component: SettingsPage
  }
} satisfies Record<AppRoute, {
  label: string;
  title: string;
  description: string;
  icon: LucideIcon;
  component: ComponentType<RouteProps>;
}>;

export const routeIds = Object.keys(routes) as AppRoute[];
