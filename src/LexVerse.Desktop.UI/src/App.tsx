import { useState } from "react";
import { routes, routeIds } from "./app/routes";
import type { AppRoute } from "./app/route-types";
import { useDesktopApp } from "./app/useDesktopApp";
import "./styles.css";

export function App() {
  const app = useDesktopApp();
  const [route, setRoute] = useState<AppRoute>("workspace");
  const Page = routes[route].component;

  return (
    <main className="shell">
      <header className="masthead">
        <div className="brand-mark" aria-hidden="true">LV</div>
        <div className="brand-copy">
          <p className="eyebrow">Realtime language workspace</p>
          <h1>LexVerse</h1>
        </div>
        <nav className="top-nav" aria-label="Workspace navigation">
          {routeIds.map((id) => (
            <button key={id} className={route === id ? "nav-active" : ""} onClick={() => setRoute(id)}>
              {routes[id].label}
            </button>
          ))}
        </nav>
        <div className="hotkey"><span />{app.snapshot?.hotkey ?? "F6 starting"}</div>
      </header>

      <Page app={app} navigate={setRoute} />
    </main>
  );
}
