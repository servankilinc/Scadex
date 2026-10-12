import type { ReactNode } from 'react';
import { ChevronRightIcon } from 'lucide-react';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { cn } from '@/lib/utils';

/** Açılış/kapanış animasyonu: Base UI paneli `--collapsible-panel-height` ile ölçer. */
const PANEL_CLASS =
  'h-(--collapsible-panel-height) overflow-hidden transition-[height] duration-200 ease-out data-ending-style:h-0 data-starting-style:h-0';

/**
 * Yan paneldeki açılır-kapanır bölüm (kabin detay paneli). Başlık her zaman görünür ve tıklanınca içerik açılıp kapanır;
 * `summary` başlığın sağında durur — bölüm KAPALIYKEN de özet (durum rozeti, "canlı" göstergesi…) okunabilsin.
 *
 * Bölüm kendi çerçevesini çizer; içerik ayrıca kart/çerçeve açmamalı — iç içe çerçeveler paneli okunmaz yapıyordu. Modüllerin
 * panel bölümleri (`AppModule.CabinetPanelSection`) de bunu kullanır, panel tek tip görünür.
 */
export function PanelSection({
  title,
  summary,
  defaultOpen = true,
  children,
  className
}: {
  title: ReactNode;
  summary?: ReactNode;
  defaultOpen?: boolean;
  children: ReactNode;
  className?: string;
}) {
  return (
    <Collapsible defaultOpen={defaultOpen} className={cn('rounded-lg border bg-card', className)}>
      <CollapsibleTrigger className='group flex w-full cursor-pointer items-center gap-2 rounded-lg px-3 py-2.5 text-left hover:bg-muted/50'>
        <ChevronRightIcon className='size-4 shrink-0 text-muted-foreground transition-transform duration-200 group-data-panel-open:rotate-90' />
        <span className='flex-1 truncate text-sm font-medium'>{title}</span>
        {summary && <span className='flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground'>{summary}</span>}
      </CollapsibleTrigger>
      <CollapsibleContent className={PANEL_CLASS}>
        <div className='space-y-3 border-t px-3 py-3'>{children}</div>
      </CollapsibleContent>
    </Collapsible>
  );
}

/** Bölüm içindeki etiket–değer satırı (künye alanları): solda soluk etiket, sağda değer. */
export function PanelField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className='grid grid-cols-[6rem_1fr] items-start gap-2 text-sm'>
      <span className='text-muted-foreground'>{label}</span>
      <span className='min-w-0 break-words'>{children}</span>
    </div>
  );
}
