import { useState } from 'react';
import { CopyIcon, PowerIcon, Trash2Icon } from 'lucide-react';
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuGroup,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger
} from '@/components/ui/context-menu';
import { useSendCommand } from '@/hooks/use-device-commands';
import { useDiagramCanvasContext } from '@/lib/diagram/canvas-context';
import { useIsUnsaved } from '@/lib/diagram/unsaved-store';
import type { DiagramDeviceDto } from '@/models/diagram';
import type { DeviceCommandSendRequest } from '@/models/deviceCommand';
import { DeviceCommandType, PinDirection } from '@/models/enums';
import { ConfirmDeleteDialog } from './confirm-delete';

/**
 * Cihaza sağ tık menüsü: kumanda + düzenleme.
 *
 * **Menü, sunucunun ön kontrollerini ÖNCEDEN söyler.** Dış kodu olmayan bir
 * cihaza ya da SCADA'sı kapalı bir kabine komut göndermek 400 döner; kullanıcıya
 * bir hata mesajı yerine sebebi menüde göstermek, tıklanabilir görünüp
 * başarısız olan bir menüden iyidir. Bu doğrulamanın YERİNE GEÇMEZ — sunucu
 * kontrolleri yerinde duruyor ve tek gerçek engel onlar.
 *
 * **Düzenleme bölümü kumanda engellense de görünür.** Engellerin en sık görüleni
 * "cihaz henüz kaydedilmedi" ve yanlışlıkla bırakılmış bir cihazı silmek isteme
 * ihtimalinin en yüksek olduğu an tam olarak orası. İki bölümü aynı koşula
 * bağlamak, yeni bırakılan cihazı menüden silinemez yapardı.
 *
 * Sözleşme: `Backend/docs/api-contract/08-scada-command.md`
 */

export function DeviceNodeMenu({ device, children }: { device: DiagramDeviceDto; children: React.ReactNode }) {
  const context = useDiagramCanvasContext();
  const send = useSendCommand(device.id);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  // Erken dönüşten ÖNCE: hook sırası koşullu olamaz.
  const isUnsaved = useIsUnsaved(device.id);

  if (!context) return <>{children}</>;

  // Giriş yönlü kanal kumanda hedefi olamaz (sunucu 400 döner) — dijital `Input`
  // ve `AnalogInput` birlikte dışarıda. Bidirectional GEÇERLİDİR: adı gereği
  // çıkış da verebilir. Koşul `DeviceCommandService.Send`'deki kontrolün aynısı
  // olmalı; sapma buradaki butonu görünür yapıp isteği 400'e düşürürdü.
  const targets = device.ioChannels.filter(
    channel =>
      channel.isEnabled &&
      channel.direction !== PinDirection.Input &&
      channel.direction !== PinDirection.AnalogInput
  );

  const blocker = findBlocker(device, context.scadaIsEnabled, isUnsaved);

  const dispatch = (request: DeviceCommandSendRequest) => send.mutate(request);

  return (
    <>
      <ContextMenu>
        <ContextMenuTrigger className='size-full'>{children}</ContextMenuTrigger>

        <ContextMenuContent className='min-w-52'>
          {/* Düz <div>: `ContextMenuLabel` = Base UI `Menu.GroupLabel` ve bir
              GRUBU etiketlemek zorunda (Radix'in serbest Label'i değil) —
              grupsuz kullanım çalışma anında patlar, derleyici uyarmaz. Cihaz
              adı bir grubun başlığı değil, menünün başlığı. Aşağıdaki iki başlık
              ise gerçek grup etiketi, o yüzden `ContextMenuGroup` içindeler. */}
          <div className='text-muted-foreground truncate px-1.5 py-1 text-xs font-medium'>{device.name}</div>
          <ContextMenuSeparator />

          <ContextMenuGroup>
            <ContextMenuLabel>Kumanda</ContextMenuLabel>
            {blocker ? (
              <p className='text-muted-foreground px-1.5 py-1 text-xs'>{blocker}</p>
            ) : targets.length === 0 ? (
              <p className='text-muted-foreground px-1.5 py-1 text-xs'>Bu cihazda çıkış yönlü kanal yok.</p>
            ) : (
              targets.map(channel => (
                <ContextMenuSub key={channel.id}>
                  <ContextMenuSubTrigger>
                    <PowerIcon />
                    <span className='truncate'>
                      OUT{channel.channelNumber} · {channel.name}
                    </span>
                  </ContextMenuSubTrigger>
                  {/* Gönderilen NİYET, ham değer değil: telde gidecek "1"/"0"'ı
                      sunucu NO/NC kutbuna göre çözüyor. Burada "1" göndermek, NC
                      kablolu bir röleyi tam ters yönde sürerdi. */}
                  <ContextMenuSubContent>
                    <ContextMenuItem onClick={() => dispatch({ commandType: DeviceCommandType.SetOutput, ioChannelId: channel.id, turnOn: true })}>
                      Aç
                    </ContextMenuItem>
                    <ContextMenuItem onClick={() => dispatch({ commandType: DeviceCommandType.SetOutput, ioChannelId: channel.id, turnOn: false })}>
                      Kapat
                    </ContextMenuItem>
                  </ContextMenuSubContent>
                </ContextMenuSub>
              ))
            )}
          </ContextMenuGroup>

          <ContextMenuSeparator />

          <ContextMenuGroup>
            <ContextMenuLabel>Düzenle</ContextMenuLabel>
            <ContextMenuItem onClick={() => context.onDuplicate(device.id)}>
              <CopyIcon />
              Kopyala
            </ContextMenuItem>
            {/* Silme EN ALTTA ve onaylı: geri alma yok (D3'te iptal edildi), tek
                yanlış tıklama cihazı kablolarıyla birlikte götürür. */}
            <ContextMenuItem variant='destructive' onClick={() => setConfirmingDelete(true)}>
              <Trash2Icon />
              Sil
            </ContextMenuItem>
          </ContextMenuGroup>
        </ContextMenuContent>
      </ContextMenu>

      {confirmingDelete && (
        <ConfirmDeleteDialog
          title='Cihazı sil'
          summary={
            <>
              <span className='font-medium'>{device.name}</span> silinecek. Bu cihaza bağlı kablolar da silinir ve geri alınamaz.
            </>
          }
          onCancel={() => setConfirmingDelete(false)}
          onConfirm={() => {
            setConfirmingDelete(false);
            context.onDelete(device.id);
          }}
        />
      )}
    </>
  );
}

/**
 * Menünün neden kapalı olduğunu tek bir cümleyle söyler.
 *
 * Sıra sunucudaki ön kontrol sırasıyla aynı tutuldu ki kullanıcı önce hangi
 * engelle karşılaşacaksa onu görsün.
 */
function findBlocker(device: DiagramDeviceDto, scadaIsEnabled: boolean, isUnsaved: boolean): string | null {
  // Kaydedilmemişlik Id'den OKUNAMAZ (Guid'i istemci üretiyor); çağıran taraf
  // `useIsUnsaved` ile okuyup buraya geçirir.
  if (isUnsaved) return 'Cihaz henüz kaydedilmedi. Kaydettikten sonra kumanda gönderilebilir.';
  if (!scadaIsEnabled) return 'Bu kabinde SCADA kapalı; kumanda gönderilemez.';
  if (!device.externalCode) return 'Cihazın dış kodu yok — SCADA onu tanımaz. Özellikler panelinden ekleyin.';
  return null;
}
