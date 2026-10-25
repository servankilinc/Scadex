import { useRef, useState } from 'react';
import { CopyIcon, LoaderIcon, SendIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import type { ClipboardResult } from '../models/clipboard';

/** Pano metninin üst sınırı — sunucudaki sınırla aynı (RemoteDesk.md § 12.9). */
export const CLIPBOARD_MAX_CHARS = 1_000_000;

type ClipboardDialogProps =
  | { mode: 'export'; onSend: (text: string) => Promise<ClipboardResult>; onClose: () => void }
  | { mode: 'import'; text: string; onClose: () => void };

/**
 * Panolar arası aktarımın İKİ ADIMLI yolu (2026-10-26, kullanıcı kararı: kapalı ağda HTTPS zorunlu değil). `navigator.clipboard` yalnızca
 * güvenli bağlamda (HTTPS/localhost) vardır; HTTP'de tek tık yerine bu pencere açılır:
 * - **Dışa aktar:** operatör metni kutuya Ctrl+V ile yapıştırır, "Uzak PC'ye gönder" der.
 * - **İçe aktar:** uzak metin seçili gelir; "Kopyala" (`document.execCommand('copy')` — güvenli bağlam istemez, kullanıcı tıklamasıyla
 *   çalışır) ya da Ctrl+C.
 * Klavye ile yapıştırma/kopyalama her bağlamda çalıştığı için bu yol HTTP'de de güvenilirdir. İçerik loglanmaz (parola olabilir).
 */
export function ClipboardDialog(props: ClipboardDialogProps) {
  const exporting = props.mode === 'export';
  const [text, setText] = useState(exporting ? '' : props.text);
  const [sending, setSending] = useState(false);
  const areaRef = useRef<HTMLTextAreaElement | null>(null);
  const tooLong = text.length > CLIPBOARD_MAX_CHARS;

  const send = async () => {
    if (props.mode !== 'export' || tooLong) return;
    setSending(true);
    try {
      const res = await props.onSend(text);
      if (res.success) {
        toast.success('Pano uzak PC’ye aktarıldı.');
        props.onClose();
      } else {
        toast.error(res.message ?? 'Pano aktarılamadı.');
      }
    } finally {
      setSending(false);
    }
  };

  const copy = () => {
    const area = areaRef.current;
    if (!area) return;
    area.focus();
    area.select();
    // execCommand eskidir ama güvenli bağlam istemeyen tek tıkla kopyalama yoludur; başarısızsa metin seçili kalır, Ctrl+C yeter.
    const ok = document.execCommand('copy');
    if (ok) {
      toast.success('Uzak pano yerele alındı.');
      props.onClose();
    } else {
      toast.error('Kopyalanamadı — metin seçili, Ctrl+C ile kopyalayın.');
    }
  };

  return (
    <Dialog open onOpenChange={open => !open && !sending && props.onClose()}>
      <DialogContent className='flex w-full max-w-[calc(100%-2rem)] flex-col gap-3 sm:max-w-2xl'>
        <DialogHeader>
          <DialogTitle>{exporting ? 'Panoyu uzak PC’ye aktar' : 'Uzak panoyu al'}</DialogTitle>
          <DialogDescription>
            {exporting
              ? 'Metni aşağıya yapıştırın (Ctrl+V) ve gönderin.'
              : 'Uzak PC panosundaki metin aşağıda seçili. “Kopyala”ya basın ya da Ctrl+C ile kopyalayın.'}
          </DialogDescription>
        </DialogHeader>

        <Textarea
          ref={areaRef}
          autoFocus
          readOnly={!exporting}
          value={text}
          onChange={e => setText(e.target.value)}
          onFocus={e => !exporting && e.currentTarget.select()}
          placeholder={exporting ? 'Ctrl+V ile yapıştırın' : 'Uzak panoda metin yok.'}
          className='min-h-48 font-mono text-xs'
        />
        {tooLong && <p className='text-xs text-destructive'>Metin çok uzun (en fazla ~1 milyon karakter).</p>}

        <DialogFooter>
          <Button variant='outline' onClick={props.onClose} disabled={sending}>
            Kapat
          </Button>
          {exporting ? (
            <Button onClick={() => void send()} disabled={sending || text.length === 0 || tooLong}>
              {sending ? <LoaderIcon className='animate-spin' /> : <SendIcon />}
              Uzak PC’ye gönder
            </Button>
          ) : (
            <Button onClick={copy} disabled={text.length === 0}>
              <CopyIcon />
              Kopyala
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
