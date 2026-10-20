import { HelpTip } from "./HelpTip";
import { helpText } from "./helpText";

export function OptionHelp({ label }: { label: string }) {
  const text = helpText(label);
  return <HelpTip label={label}>{text}</HelpTip>;
}
