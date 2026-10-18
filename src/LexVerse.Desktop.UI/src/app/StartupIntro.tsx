import { useEffect, useRef, useState } from "react";
import "./startup-intro.css";

export function StartupIntro({ ready, onComplete }: { ready: boolean; onComplete: () => void }) {
  const overlay = useRef<HTMLDivElement>(null);
  const video = useRef<HTMLVideoElement>(null);
  const [phase, setPhase] = useState<"playing" | "holding" | "fading">("playing");

  useEffect(() => {
    const surface = overlay.current;
    if (!surface) return;
    const blockWheel = (event: WheelEvent) => {
      event.preventDefault();
      event.stopPropagation();
    };
    surface.addEventListener("wheel", blockWheel, { passive: false });
    return () => surface.removeEventListener("wheel", blockWheel);
  }, []);

  useEffect(() => {
    const player = video.current;
    if (!player) return;
    player.volume = 0.1; // Âm lượng 30%
    void player.play().catch(() => setPhase("holding"));
    // A missing codec or stalled asset must not trap the user in the intro.
  }, []);

  useEffect(() => {
    if (phase !== "playing") return;
    let lastTime = -1;
    const watchdog = window.setInterval(() => {
      const player = video.current;
      if (!player) return;
      if (player.currentTime === lastTime) {
        player.pause();
        setPhase("holding");
      }
      lastTime = player.currentTime;
    }, 10_000);
    return () => window.clearInterval(watchdog);
  }, [phase]);

  useEffect(() => {
    if (phase !== "holding" || !ready) return;
    const hold = window.setTimeout(() => setPhase("fading"), 700);
    return () => window.clearTimeout(hold);
  }, [phase, ready]);

  useEffect(() => {
    if (phase !== "fading") return;
    const fade = window.setTimeout(onComplete, 800);
    return () => window.clearTimeout(fade);
  }, [phase, onComplete]);

  return (
    <div
      ref={overlay}
      className={`startup-intro startup-intro-${phase}`}
      role="status"
      aria-label="Starting LexVerse"
      onPointerDown={blockInteraction}
      onPointerUp={blockInteraction}
      onMouseDown={blockInteraction}
      onMouseUp={blockInteraction}
      onClick={blockInteraction}
      onDoubleClick={blockInteraction}
      onAuxClick={blockInteraction}
      onContextMenu={blockInteraction}
      onDragStart={blockInteraction}
    >
      <video
        ref={video}
        src="/videos/lexverse_intro.mp4"
        autoPlay
        playsInline
        preload="auto"
        disablePictureInPicture
        controls={false}
        draggable={false}
        tabIndex={-1}
        onEnded={() => {
          // Pause without seeking or unloading: Chromium retains the final frame.
          video.current?.pause();
          setPhase("holding");
        }}
        onError={() => setPhase("holding")}
      />
    </div>
  );
}

function blockInteraction(event: { preventDefault: () => void; stopPropagation: () => void }) {
  event.preventDefault();
  event.stopPropagation();
}
