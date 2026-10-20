import { useEffect, useRef } from "react";

import settings from "./guide-assets/settings.json";
import consent from "./guide-assets/consent.json";
import languages from "./guide-assets/languages.json";
import text from "./guide-assets/text.json";
import popup from "./guide-assets/popup.json";
import app from "./guide-assets/app.json";
import region from "./guide-assets/region.json";
import screen from "./guide-assets/screen.json";
import stop from "./guide-assets/stop.json";
const illustrations: Record<string, unknown> = { settings, consent, languages, text, popup, app, region, screen, stop };

// Local vector artwork: a friendly information bubble and a pointing cursor.
const artwork = (demo: boolean) => ({
  v: "5.12.2", fr: 30, ip: 0, op: 60, w: 120, h: 90, nm: "LexVerse guide", ddd: 0, assets: [],
  layers: [{ ddd: 0, ind: 1, ty: 4, nm: "Information friend", sr: 1, ip: 0, op: 60, st: 0,
    ks: { o: { a: 0, k: 100 }, r: { a: 0, k: 0 }, p: { a: 1, k: [
      { t: 0, s: [60, 47, 0], e: [60, 39, 0], o: { x: .4, y: 0 }, i: { x: .6, y: 1 } },
      { t: 30, s: [60, 39, 0], e: [60, 47, 0], o: { x: .4, y: 0 }, i: { x: .6, y: 1 } }, { t: 60, s: [60, 47, 0] }] },
      a: { a: 0, k: [0, 0, 0] }, s: { a: 0, k: [100, 100, 100] } },
    shapes: [{ ty: "gr", it: [
      { ty: "el", p: { a: 0, k: [0, 0] }, s: { a: 0, k: [demo ? 65 : 70, demo ? 65 : 70] } },
      { ty: "fl", c: { a: 0, k: [.16, .46, .32, 1] }, o: { a: 0, k: 100 }, r: 1 }] }, { ty: "gr", it: [
      { ty: "rc", p: { a: 0, k: [0, 5] }, s: { a: 0, k: [7, 22] }, r: { a: 0, k: 3 } },
      { ty: "el", p: { a: 0, k: [0, -14] }, s: { a: 0, k: [8, 8] } },
      { ty: "fl", c: { a: 0, k: [1, 1, 1, 1] }, o: { a: 0, k: 100 }, r: 1 }] }
    ] }]
});

export function GuideAnimation({ demo = false, variant = "text" }: { demo?: boolean; variant?: string }) {
  const container = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    let disposed = false;
    let animation: import("lottie-web").AnimationItem | undefined;
    const motion = window.matchMedia?.("(prefers-reduced-motion: reduce)");
    void import("lottie-web/build/player/lottie_light").then(({ default: lottie }) => {
      if (disposed || !container.current) return;
      animation = lottie.loadAnimation({ container: container.current, renderer: "svg", loop: true,
        autoplay: !motion?.matches, animationData: demo ? illustrations[variant] ?? illustrations.text : artwork(false) });
      if (motion?.matches) animation.goToAndStop(0, true);
    }).catch(() => { /* The readable fallback stays available. */ });
    const update = () => { if (motion?.matches) animation?.goToAndStop(0, true); else animation?.play(); };
    motion?.addEventListener("change", update);
    return () => { disposed = true; animation?.destroy(); motion?.removeEventListener("change", update); };
  }, [demo, variant]);
  return <span className={demo ? "guide-animation guide-demo" : "guide-animation"} aria-hidden="true"><span className="guide-animation-fallback">i</span><span ref={container} /></span>;
}
