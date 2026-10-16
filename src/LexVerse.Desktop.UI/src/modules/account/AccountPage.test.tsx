import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { DesktopBridge } from "../../bridge";
import type { RouteProps } from "../../app/route-types";
import type { DesktopSnapshot } from "../../types";
import { AccountPage } from "./AccountPage";

afterEach(cleanup);

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
  expect(screen.getByRole("button", { name: /Sign in or create account/ })).toBeEnabled();
  expect(screen.queryByRole("button", { name: "Copy link" })).not.toBeInTheDocument();
});
