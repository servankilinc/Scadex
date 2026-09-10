import { CheckIcon, LoaderCircleIcon, TriangleAlertIcon, WifiOffIcon } from 'lucide-react';
import { useDeviceCommands } from '@/hooks/use-device-commands';
import { useIsUnsaved } from '@/lib/diagram/unsaved-store';
import { CommandStatus, CommandStatusLabels, DeviceCommandTypeLabels } from '@/models/enums';
import type { DeviceCommandResultDto } from '@/models/deviceCommand';
import { cn } from '@/lib/utils';

/**
 * Seçili cihazın son kumandaları.
 *
 * **`Failed` ile `NoResponse` görsel olarak AYRIŞTIRILIR.** İkisi de
 * "başarısız" ama anlamları farklı: `Failed`'da komutun sahaya gitmediği
 * kesindir, `NoResponse`'ta gidip gitmediği bilinmez. Operatörün yeniden
 * denemeden önce bilmesi gereken şey tam olarak budur; tek bir kırmızı rozet
 * altında birleştirmek o bilgiyi yok ederdi.
 *
 * Sözleşme: `Backend/docs/api-contract/08-scada-command.md`
 */
export function CommandHistory({ deviceId }: { deviceId: string }) {
  const { data, isPending, isError } = useDeviceCommands(deviceId);
  const isUnsaved = useIsUnsaved(deviceId);

  // Kaydedilmemiş cihaz için sorgu hiç açılmaz; geçmiş de olamaz.
  if (isUnsaved) return null;

  return (
    <div className='flex flex-col gap-1.5 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>Son kumandalar</p>
      {renderBody()}
    </div>
  );

  function renderBody() {
    if (isPending) return <p className='text-muted-foreground text-xs'>Yükleniyor…</p>;
    if (isError) return <p className='text-destructive text-xs'>Kumanda geçmişi alınamadı.</p>;
    if (!data || data.length === 0) {
      return <p className='text-muted-foreground text-xs'>Bu cihaza henüz kumanda gönderilmedi. Sağ tıklayarak gönderebilirsiniz.</p>;
    }

    return (
      <ul className='flex flex-col gap-1.5'>
        {data.map(command => (
          <CommandRow key={command.id} command={command} />
        ))}
      </ul>
    );
  }
}

function CommandRow({ command }: { command: DeviceCommandResultDto }) {
  const { icon: Icon, tone } = STATUS_STYLE[command.status];

  return (
    <li className='flex flex-col gap-0.5 text-xs'>
      <div className='flex items-baseline gap-1.5'>
        <Icon className={cn('size-3 shrink-0 translate-y-0.5', tone, command.status === CommandStatus.Sent && 'animate-spin')} />
        <span className='truncate font-medium'>
          {DeviceCommandTypeLabels[command.commandType]}
          {command.channelNumber != null && <span className='text-muted-foreground font-mono'> · CH{command.channelNumber}</span>}
        </span>
        <span className='text-muted-foreground ml-auto shrink-0 font-mono text-[10px]' title={command.sentAt ? new Date(command.sentAt).toLocaleString('tr-TR') : undefined}>
          {command.sentAt ? new Date(command.sentAt).toLocaleTimeString('tr-TR') : '—'}
        </span>
      </div>

      <div className='text-muted-foreground flex items-baseline gap-1.5 pl-4.5'>
        <span className={tone}>{CommandStatusLabels[command.status]}</span>
        {command.elapsedMs != null && <span className='font-mono text-[10px]'>{command.elapsedMs} ms</span>}
        {command.requestedByName && <span className='ml-auto truncate text-[10px]'>{command.requestedByName}</span>}
      </div>

      {/* Teşhis metni yalnızca başarısızlıkta: başarıda SCADA'nın gövdesi
          genellikle boş ve dolu olduğunda da operatöre bir şey söylemiyor. */}
      {command.status !== CommandStatus.Succeeded && command.resultMessage && (
        <p className='text-muted-foreground pl-4.5 text-[10px] break-words' title={command.resultMessage}>
          {command.resultMessage}
        </p>
      )}
    </li>
  );
}

const STATUS_STYLE: Record<CommandStatus, { icon: typeof CheckIcon; tone: string }> = {
  [CommandStatus.Sent]: { icon: LoaderCircleIcon, tone: 'text-muted-foreground' },
  [CommandStatus.Succeeded]: { icon: CheckIcon, tone: 'text-emerald-600 dark:text-emerald-500' },
  // SCADA cevap verdi ve komutu almadı — komut sahaya GİTMEDİ.
  [CommandStatus.Failed]: { icon: TriangleAlertIcon, tone: 'text-destructive' },
  // SCADA sessiz — komutun sahaya gidip gitmediği BİLİNMİYOR. Farklı bir ikon,
  // tam olarak bu belirsizliği göstermek için.
  [CommandStatus.NoResponse]: { icon: WifiOffIcon, tone: 'text-amber-600 dark:text-amber-500' }
};
