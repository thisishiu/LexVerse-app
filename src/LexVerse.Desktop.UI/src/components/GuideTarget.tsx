import { useLayoutEffect, useRef } from "react";
import { helpId, helpText } from "./guideCatalog";

// A declaration registers its whole control/group; it never renders an inline icon.
const groups = ".action-with-help, .field-stack, .language-field, .setting-row, .range-setting, .shortcut-setting-row, .workspace-ocr-toolbar, .settings-save-area, .palette-setting, .offer-with-help, .window-choice-row, .heading-with-help, .verified-payment, .settings-form-section";
export function GuideTarget({ children, label, id, target, demoKey }: { children?: string; label: string; id?: string; target?: string; demoKey?: string }) {
  const marker = useRef<HTMLSpanElement>(null);
  useLayoutEffect(() => {
    const element = ((target ? marker.current?.closest(target) : marker.current?.closest(groups)) as HTMLElement | null)
      ?? marker.current?.parentElement;
    if (!element) return;
    element.dataset.helpId = id ?? helpId(label);
    element.dataset.helpTitle = label;
    element.dataset.helpText = helpText(label, children);
    if (demoKey) element.dataset.helpDemo = demoKey;
    return () => { delete element.dataset.helpId; delete element.dataset.helpTitle; delete element.dataset.helpText; delete element.dataset.helpDemo; };
  }, [children, label, id, target, demoKey]);
  return <span ref={marker} hidden aria-hidden="true" />;
}
