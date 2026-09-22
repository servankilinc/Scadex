import type { KeyboardEvent, ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * SVG içindeki tıklanabilir cihaz. `<button>` kullanılamaz (SVG'nin içindeyiz), bu yüzden erişilebilirlik elle kurulur:
 * `role`, `tabIndex`, `aria-label` ve Enter/Space.
 *
 * `onActivate` verilmezse cihaz yalnızca izlenir: odak almaz, imleç değişmez.
 */
interface FigureButtonProps {
  label: string;
  onActivate?: () => void;
  /** Komut gönderilemeyecek durumda (kanal tanımsız vb.): soluk çizilir, tıklanamaz. */
  disabled?: boolean;
  transform?: string;
  className?: string;
  children: ReactNode;
}

export function FigureButton({ label, onActivate, disabled = false, transform, className, children }: FigureButtonProps) {
  const isInteractive = Boolean(onActivate) && !disabled;

  const handleKeyDown = (event: KeyboardEvent<SVGGElement>) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    onActivate?.();
  };

  return (
    <g
      transform={transform}
      role={isInteractive ? 'button' : 'img'}
      aria-label={label}
      aria-disabled={disabled || undefined}
      tabIndex={isInteractive ? 0 : undefined}
      onClick={isInteractive ? onActivate : undefined}
      onKeyDown={isInteractive ? handleKeyDown : undefined}
      className={cn(
        'origin-center outline-none transition-opacity',
        disabled && 'opacity-40',
        isInteractive && 'cursor-pointer hover:opacity-80 focus-visible:opacity-80',
        className
      )}
    >
      <title>{label}</title>
      {children}
    </g>
  );
}
