import { useEffect, useEffectEvent } from 'react';

/**
 * Kasadaki bir cihaz kutusunu (`<g id="…-Box">`) erişilebilir bir düğmeye çevirir — `FigureButton`'ın DOM karşılığı.
 * Kutu React'in değil dosyanın elemanı olduğu için özellikler ve dinleyiciler effect ile takılır.
 *
 * `onActivate` verilmezse cihaz yalnızca izlenir: odak almaz, imleç değişmez. `disabled` (kanal tanımsız vb.) soluk çizer.
 */
interface SvgDeviceOptions {
  label: string;
  onActivate?: () => void;
  disabled?: boolean;
}

const INTERACTIVE_CLASSES = ['cursor-pointer', 'outline-none', 'transition-opacity', 'hover:opacity-80', 'focus-visible:opacity-80'];
const DISABLED_CLASS = 'opacity-40';

export function useSvgDevice(element: SVGGraphicsElement | undefined, { label, onActivate, disabled = false }: SvgDeviceOptions): void {
  const isInteractive = Boolean(onActivate) && !disabled;
  // İşleyici her render'da yenilenir; dinleyicileri yeniden takmadan güncelini çağırmak için.
  const activate = useEffectEvent(() => onActivate?.());

  useEffect(() => {
    if (!element) return;

    element.setAttribute('role', isInteractive ? 'button' : 'img');
    element.setAttribute('aria-label', label);
    toggleAttribute(element, 'aria-disabled', disabled ? 'true' : null);
    toggleAttribute(element, 'tabindex', isInteractive ? '0' : null);

    element.classList.toggle(DISABLED_CLASS, disabled);
    for (const className of INTERACTIVE_CLASSES) element.classList.toggle(className, isInteractive);

    setTooltip(element, label);
  }, [element, label, isInteractive, disabled]);

  useEffect(() => {
    if (!element || !isInteractive) return;

    const handleClick = () => activate();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Enter' && event.key !== ' ') return;
      event.preventDefault();
      activate();
    };

    element.addEventListener('click', handleClick);
    element.addEventListener('keydown', handleKeyDown);
    return () => {
      element.removeEventListener('click', handleClick);
      element.removeEventListener('keydown', handleKeyDown);
    };
  }, [element, isInteractive]);
}

function toggleAttribute(element: Element, name: string, value: string | null): void {
  if (value === null) element.removeAttribute(name);
  else element.setAttribute(name, value);
}

/** Tarayıcı ipucu: kutunun ilk çocuğu olan `<title>` (dosyada yoksa eklenir). */
function setTooltip(element: SVGGraphicsElement, label: string): void {
  let title = Array.from(element.children).find(child => child.localName === 'title');
  if (!title) {
    title = element.ownerDocument.createElementNS('http://www.w3.org/2000/svg', 'title');
    element.prepend(title);
  }
  title.textContent = label;
}
