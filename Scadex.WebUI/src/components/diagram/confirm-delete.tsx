import { useState } from 'react';
import { Trash2Icon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';

/**
 * Silme onayı.
 *
 * Onay adımı bilinçli bir sürtünme: undo/redo bu turda iptal edildi, dolayısıyla
 * yanlışlıkla silinen bir öğeyi geri getirmenin tek yolu Kaydet'e basmadan
 * sayfadan çıkmak — yani o oturumdaki BÜTÜN işi atmak.
 *
 * Sağ tık menüsü ve özellikler paneli aynı diyaloğu kullanıyor. İkisine ayrı
 * metin yazmak, aynı işlemin nereden başlatıldığına göre farklı sonuç
 * doğurduğu izlenimini verirdi.
 *
 * `Dialog` kullanılıyor, `AlertDialog` değil: ikincisi Escape ile kapanmayı
 * engelliyor ve buradaki karar geri dönülemez olsa da acil değil.
 */
export function ConfirmDeleteDialog({
  title,
  summary,
  onCancel,
  onConfirm
}: {
  title: string;
  summary: React.ReactNode;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Dialog open onOpenChange={next => !next && onCancel()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{summary}</DialogDescription>
        </DialogHeader>

        <p className='text-muted-foreground text-xs'>
          {/* Silme sunucuya HEMEN gitmiyor: kaydedilene kadar yalnızca canvas'ta
              yok. Kullanıcının "yanlış sildim" anında elinde bir çıkış olduğunu
              bilmesi, onayı gereksiz yere korkutucu olmaktan çıkarıyor. */}
          Değişiklik Kaydet'e basılana kadar sunucuya gönderilmez.
        </p>

        <DialogFooter>
          <DialogClose render={<Button variant='outline' size='sm' />}>Vazgeç</DialogClose>
          <Button size='sm' variant='destructive' onClick={onConfirm}>
            Sil
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Onay diyaloğunu kendi açan silme düğmesi — panelin kullandığı biçim. */
export function DeleteButton({ label, title, summary, onConfirm }: { label: string; title: string; summary: React.ReactNode; onConfirm: () => void }) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button
        size='sm'
        variant='outline'
        // Kırmızı çerçeveli ama dolgusuz: panelde sürekli duran bir düğme,
        // dolu kırmızıyla çizilseydi ekranın en dikkat çeken öğesi olurdu.
        className='text-destructive hover:text-destructive border-destructive/40 hover:bg-destructive/10'
        onClick={() => setOpen(true)}>
        <Trash2Icon />
        {label}
      </Button>

      {open && (
        <ConfirmDeleteDialog
          title={title}
          summary={summary}
          onCancel={() => setOpen(false)}
          onConfirm={() => {
            setOpen(false);
            onConfirm();
          }}
        />
      )}
    </>
  );
}
