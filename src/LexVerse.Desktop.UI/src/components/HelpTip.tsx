import { useId, useRef, useState } from "react";
import { Info } from "lucide-react";
export function HelpTip({ children, label }: { children: string; label: string }) {
 const id = useId();
 const [open, setOpen] = useState(false);
 const pointerFocus = useRef(false);
 return <span className="help-tip" onPointerEnter={event => { if (event.pointerType !== "touch") setOpen(true); }}
 onPointerLeave={event => { if (event.pointerType !== "touch") setOpen(false); }}
 onKeyDown={event => { if (event.key === "Escape") { setOpen(false); event.stopPropagation(); } }}>
 <button type="button" className="help-trigger" aria-label={label} aria-describedby={open ? id : undefined}
 onPointerDown={() => { pointerFocus.current = true; }}
 onFocus={() => { if (!pointerFocus.current) setOpen(true); }}
 onBlur={() => { setOpen(false); pointerFocus.current = false; }}
 onClick={event => { if (event.detail === 0) setOpen(true); else setOpen(value => !value); pointerFocus.current = false; }}>
 <Info size={16} aria-hidden="true" /></button>
 {open && <span className="help-content" id={id} role="tooltip">{children}</span>}</span>;
}
