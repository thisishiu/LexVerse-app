import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DesktopBridge } from "../../bridge";
import type { RouteProps } from "../../app/route-types";
import type { DesktopSnapshot } from "../../types";
import { AccountPage } from "./AccountPage";

afterEach(cleanup);

it("shows sync progress, rejects failure inline, and displays confirmed Free after retry", async () => {
  const preview = new DesktopBridge();
  const snapshot = await preview.command<DesktopSnapshot>("app.getSnapshot");
  preview.dispose();
  snapshot.settings.appLanguage = "en";
  snapshot.commerce = { configured: true, status: "SIGNED IN", displayName: "Reader", offers: [] };
  let reject!: (reason: Error) => void;
  const command = vi.fn().mockImplementationOnce(() => new Promise((_resolve, failure) => { reject = failure; }))
    .mockResolvedValueOnce(null);
  const app = { snapshot, bridge: { command }, run: async (_label: string, action: () => Promise<unknown>) => {
    try { await action(); } catch { /* The app shows the global bridge error separately. */ }
  } } as unknown as RouteProps["app"];
  const { rerender } = render(<AccountPage app={app} navigate={vi.fn()} />);
  fireEvent.click(screen.getByRole("button", { name: "Sync access" }));
  expect(screen.getByRole("button", { name: "Syncing…" })).toBeDisabled();
  reject(new Error("subscription-sign-in-required"));
  await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Sign in again"));
  expect(screen.getByRole("region", { name: "Current plan" })).toHaveTextContent("Could not load plan");
  expect(screen.queryByText("Plan synced successfully.")).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Sync access" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Sync access" })).toBeEnabled());
  snapshot.commerce.subscription = { current: { id: "free", planId: "free", displayName: "Free",
    startsAtUtc: "2026-10-01T00:00:00Z", endsAtUtc: "2026-11-01T00:00:00Z" }, upcoming: [] };
  snapshot.commerce.lastSyncedAtUtc = "2026-10-23T12:00:00Z";
  rerender(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("region", { name: "Current plan" })).toHaveTextContent("Free");
  expect(screen.getByText("Plan synced successfully.")).toBeInTheDocument();
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  snapshot.commerce.subscriptionError = "subscription-unavailable";
  snapshot.settings.appLanguage = "vi";
  rerender(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("region", { name: "Gói đang dùng" })).toHaveTextContent("Free");
  expect(screen.getByRole("alert")).toHaveTextContent("Hãy kiểm tra kết nối rồi đồng bộ lại");
});

it("distinguishes a failed catalog from an empty catalog and permits retry while signed out", async () => {
  const preview = new DesktopBridge();
  const snapshot = await preview.command<DesktopSnapshot>("app.getSnapshot");
  preview.dispose();
  snapshot.settings.appLanguage = "en";
  snapshot.commerce = { configured: true, status: "SIGNED OUT", offers: [], catalogError: true };
  const command = vi.fn().mockResolvedValue(null);
  const app = { snapshot, bridge: { command }, selectedOffer: "", setSelectedOffer: vi.fn(),
    run: async (_label: string, action: () => Promise<unknown>) => action() } as unknown as RouteProps["app"];
  render(<AccountPage app={app} navigate={vi.fn()} />);
  fireEvent.click(screen.getByRole("button", { name: "Browse plans" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Could not refresh plans");
  expect(screen.queryByText("No plans available")).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Retry" }));
  expect(command).toHaveBeenCalledWith("commerce.restore");
});

it("shows the granted plan separately from an unpaid selected offer and upcoming periods", async () => {
  const preview = new DesktopBridge();
  const snapshot = await preview.command<DesktopSnapshot>("app.getSnapshot");
  preview.dispose();
  snapshot.settings.appLanguage = "en";
  snapshot.commerce = { configured: true, status: "CHECKOUT", displayName: "Reader",
    offers: [{ id: "pro:1", label: "Pro · 50,000 VND" }],
    subscription: { current: { id: "free", planId: "free", displayName: "Free", startsAtUtc: "2026-10-01T00:00:00Z", endsAtUtc: "2026-11-01T00:00:00Z" }, upcoming: [] },
    pendingCheckout: { checkoutUri: "https://pay.example.test", expiresAtUtc: "2026-10-23T12:30:00Z" } };
  const app = { snapshot, bridge: { command: vi.fn() }, selectedOffer: "pro:1", setSelectedOffer: vi.fn(), run: vi.fn() } as unknown as RouteProps["app"];
  const { rerender } = render(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("region", { name: "Current plan" })).toHaveTextContent("Free");
  expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /Continue to checkout/ })).not.toBeInTheDocument();
  expect(screen.getByText(/Payment pending until/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Browse plans" }));
  expect(screen.getByRole("radio")).toBeChecked();
  expect(screen.queryByRole("region", { name: "Current plan" })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Back to account" }));
  snapshot.commerce.subscription!.current.displayName = "Pro";
  snapshot.commerce.pendingCheckout = undefined;
  snapshot.commerce.status = "SIGNED IN";
  rerender(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("region", { name: "Current plan" })).toHaveTextContent("Pro");
  expect(screen.queryByText(/Payment pending until/)).not.toBeInTheDocument();
});

it("keeps recovery controls available while waiting and hides duplicate sign-in actions", async () => {
  const preview = new DesktopBridge();
  const snapshot = await preview.command<DesktopSnapshot>("app.getSnapshot");
  preview.dispose();
  snapshot.commerce = { configured: true, status: "LOADING", offers: [], authentication: { status: "waiting", error: "browser-open-failed" } };
  snapshot.settings.appLanguage = "en";
  const command = vi.fn().mockResolvedValue(null);
  const app = {
    snapshot, bridge: { command }, busy: undefined, selectedOffer: "", setSelectedOffer: vi.fn(),
    run: async (_label: string, action: () => Promise<unknown>) => { await action(); }
  } as unknown as RouteProps["app"];
  const { rerender } = render(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("alert")).toHaveTextContent("Could not open the browser");
  expect(screen.queryByRole("button", { name: /Sign in or create account/ })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Reopen browser" }));
  fireEvent.click(screen.getByRole("button", { name: "Copy link" }));
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  expect(command.mock.calls.map(call => call[0])).toEqual(["account.reopenSignIn", "account.copySignInLink", "account.cancelSignIn"]);
  snapshot.commerce.authentication = { status: "error", error: "sign-in-cancelled-or-expired" };
  rerender(<AccountPage app={app} navigate={vi.fn()} />);
  expect(screen.getByRole("button", { name: "Sign in or create account" })).toBeEnabled();
  expect(screen.queryByRole("button", { name: "Copy link" })).not.toBeInTheDocument();
});
