import { useMemo, useState } from 'react';
import { ChevronLeftIcon, ChevronRightIcon, RotateCcwIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useCabinets } from '@/hooks/use-cabinets';
import { useChannelEvents } from '@/hooks/use-channel-events';
import { useDiagramGraph } from '@/hooks/use-diagram-graph';
import { PinDirectionLabels } from '@/models/enums';
import type { ChannelEventDto } from '@/models/channelEvent';
import { formatUtcDateTime } from '@/lib/utils';

/** Kanal filtresinde "hepsi" için sentinel — Base UI Select boş string'i "seçim yok" sayar. */
const ALL_CHANNELS = 'all';

const PAGE_SIZE = 25;

/**
 * Kanal olay geçmişi — `/events`.
 *
 * Bu ekran SALT OKUNURDUR ve canlı DEĞİLDİR. Anlık değerler diyagrama SignalR ile
 * akar; burada okunan şey geçmiştir, bu yüzden yoklama/abonelik yok — kullanıcı
 * ne zaman isterse filtreyi değiştirip yeniden sorgular.
 *
 * Kabin ZORUNLU: sunucudaki iki indeks (`CabinetId+OccurredAtUtc`,
 * `IoChannelId+OccurredAtUtc`) yalnızca kabin daraltılmış soruları cevaplayabiliyor,
 * kabinsiz istek 400 dönüyor. Bu yüzden kabin seçimi bir filtre değil, ön koşuldur.
 *
 * **Analog kanalda bu tablo hızla büyür** — dijital kanal yalnızca durum değişince
 * satır üretirken analogda pratikte her ingest bir satırdır ve temizleyen bir iş
 * yoktur (bilinçli karar, bkz. CLAUDE.md). Sayfa boyutu bu yüzden sabit ve küçük.
 */
export default function ChannelEvents() {
  const cabinets = useCabinets();

  const [selectedCabinetId, setSelectedCabinetId] = useState('');
  const [channelId, setChannelId] = useState(ALL_CHANNELS);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);

  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  // Türetme, efekt DEĞİL — `/admin/cameras` ve `/cameras` ile aynı kural:
  // `useEffect` + `setState` listenin her gelişinde bir kaskad render tetikler.
  const cabinetId = selectedCabinetId || activeCabinets[0]?.id || '';

  // Kanal listesi için diyagram anlık görüntüsü: kabindeki kanalları veren tek uç
  // bu. Sorgu anahtarı editörle ORTAK olduğundan, diyagramı açmış kullanıcıda
  // ikinci bir istek doğurmaz.
  const graph = useDiagramGraph(cabinetId || undefined);

  const channels = useMemo(() => {
    const rows = (graph.data?.devices ?? []).flatMap(device =>
      device.ioChannels.map(channel => ({ ...channel, deviceName: device.name }))
    );

    return rows.sort(
      (a, b) => a.deviceName.localeCompare(b.deviceName, 'tr') || a.direction - b.direction || a.channelNumber - b.channelNumber
    );
  }, [graph.data]);

  const events = useChannelEvents({
    cabinetId,
    ioChannelId: channelId === ALL_CHANNELS ? null : channelId,
    fromUtc: localToUtcIso(from),
    toUtc: localToUtcIso(to),
    page,
    pageSize: PAGE_SIZE
  });

  const isFiltered = channelId !== ALL_CHANNELS || from !== '' || to !== '';

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Olay Geçmişi</h1>
        <p className='text-sm text-muted-foreground'>
          SCADA'dan gelen kanal değişimleri, yeniden eskiye. Kayıtları yalnızca ingest üretir; buradan silinemez veya değiştirilemez.
        </p>
      </div>

      <div className='flex flex-wrap items-end gap-3'>
        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='event-cabinet'>Kabin</FieldLabel>
          {/* Base UI'da `onValueChange` `string | null` veriyor (temizleme durumu). */}
          <Select
            value={cabinetId}
            onValueChange={value => {
              setSelectedCabinetId(value ?? '');
              // Kabin değişince kanal filtresi ANLAMINI YİTİRİR: kanal kimlikleri
              // kabine özeldir, taşınan bir seçim boş liste gösterirdi.
              setChannelId(ALL_CHANNELS);
              setPage(1);
            }}>
            <SelectTrigger id='event-cabinet'>
              <SelectValue placeholder={cabinets.isPending ? 'Yükleniyor…' : 'Kabin seçin'} />
            </SelectTrigger>
            <SelectContent>
              {activeCabinets.map(cabinet => (
                <SelectItem key={cabinet.id} value={cabinet.id}>
                  {cabinet.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='event-channel'>Kanal</FieldLabel>
          <Select
            value={channelId}
            onValueChange={value => {
              setChannelId(value ?? ALL_CHANNELS);
              setPage(1);
            }}>
            <SelectTrigger id='event-channel' disabled={!cabinetId}>
              <SelectValue placeholder={graph.isPending ? 'Yükleniyor…' : 'Tüm kanallar'} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_CHANNELS}>Tüm kanallar</SelectItem>
              {channels.map(channel => (
                <SelectItem key={channel.id} value={channel.id}>
                  {channel.deviceName} · {channel.name} ({PinDirectionLabels[channel.direction]} {channel.channelNumber})
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='event-from'>Başlangıç</FieldLabel>
          {/* Yerel saat girilir, sunucuya UTC gider — sunucu `OccurredAtUtc`'yi
              UTC olarak saklıyor ve karşılaştırmayı damga üzerinden yapıyor. */}
          <Input
            id='event-from'
            type='datetime-local'
            value={from}
            onChange={e => {
              setFrom(e.target.value);
              setPage(1);
            }}
          />
        </Field>

        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='event-to'>Bitiş</FieldLabel>
          <Input
            id='event-to'
            type='datetime-local'
            value={to}
            onChange={e => {
              setTo(e.target.value);
              setPage(1);
            }}
          />
        </Field>

        {isFiltered && (
          <Button
            size='sm'
            variant='ghost'
            onClick={() => {
              setChannelId(ALL_CHANNELS);
              setFrom('');
              setTo('');
              setPage(1);
            }}>
            <RotateCcwIcon />
            Filtreyi temizle
          </Button>
        )}
      </div>

      {activeCabinets.length === 0 && !cabinets.isPending && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Önce bir kabin oluşturun — olaylar kabine bağlıdır.
          </CardContent>
        </Card>
      )}

      {events.isError && <p className='text-sm text-destructive'>{events.error.message}</p>}

      {events.isPending && cabinetId && <Skeleton className='h-64 w-full rounded-xl' />}

      {events.data && (
        <>
          <div className='overflow-x-auto rounded-xl border'>
            <table className='w-full min-w-[42rem] text-sm'>
              <thead className='bg-muted/50 text-muted-foreground'>
                <tr className='[&>th]:px-3 [&>th]:py-2 [&>th]:text-left [&>th]:font-medium'>
                  <th className='w-48'>Gerçekleşme</th>
                  <th>Cihaz</th>
                  <th>Kanal</th>
                  <th className='w-40'>Değişim</th>
                  <th className='w-48'>Alınma</th>
                </tr>
              </thead>
              <tbody>
                {events.data.data.map(event => (
                  <EventRow key={event.id} event={event} />
                ))}
              </tbody>
            </table>

            {events.data.data.length === 0 && (
              <p className='py-8 text-center text-sm text-muted-foreground'>
                {isFiltered ? 'Bu filtreye uyan olay yok.' : 'Bu kabinde henüz olay kaydı yok.'}
              </p>
            )}
          </div>

          <div className='flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground'>
            <span>
              {events.data.dataCount} kayıt
              {events.data.pageCount > 1 && ` · sayfa ${events.data.page}/${events.data.pageCount}`}
            </span>

            {events.data.pageCount > 1 && (
              <div className='flex items-center gap-2'>
                <Button size='sm' variant='outline' disabled={!events.data.hasPrevious} onClick={() => setPage(p => p - 1)}>
                  <ChevronLeftIcon />
                  Önceki
                </Button>
                <Button size='sm' variant='outline' disabled={!events.data.hasNext} onClick={() => setPage(p => p + 1)}>
                  Sonraki
                  <ChevronRightIcon />
                </Button>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}

function EventRow({ event }: { event: ChannelEventDto }) {
  // Damgasız ingest: sunucu `timestampUtc` gelmediğinde `OccurredAtUtc`'yi
  // `ReceivedAtUtc` ile eşitliyor. Operatör için bu, "sahadaki an bilinmiyor,
  // bu yalnızca bize ulaşma anı" demek.
  const isStampless = event.occurredAtUtc === event.receivedAtUtc;

  return (
    <tr className='border-t [&>td]:px-3 [&>td]:py-2'>
      <td className='font-mono text-xs whitespace-nowrap'>{formatUtcDateTime(event.occurredAtUtc)}</td>

      <td className='max-w-[14rem] truncate'>
        {/* Türev alanlar null = kaynak kanal silinmiş. Olay satırı DURUR (silinmiş
            bir kanalın geçmişi de delildir) ama adı artık çözülemiyor. */}
        {event.deviceName ?? <span className='text-muted-foreground italic'>silinmiş cihaz</span>}
        {event.deviceExternalCode && <span className='ml-1.5 font-mono text-xs text-muted-foreground'>{event.deviceExternalCode}</span>}
      </td>

      <td className='max-w-[14rem] truncate'>
        {event.channelName ?? <span className='text-muted-foreground italic'>silinmiş kanal</span>}
        {event.channelNumber != null && <span className='ml-1.5 font-mono text-xs text-muted-foreground'>CH{event.channelNumber}</span>}
      </td>

      <td className='font-mono text-xs whitespace-nowrap'>
        {/* İlk okumada `previousValue` null — "yoktan geldi", 0'dan değil. */}
        <span className='text-muted-foreground'>{event.previousValue ?? '—'}</span>
        <span className='mx-1.5'>→</span>
        <span className='font-medium'>{event.value}</span>
      </td>

      <td className='font-mono text-xs whitespace-nowrap'>
        {isStampless ? (
          <Badge variant='outline' className='font-sans font-normal' title='SCADA damga göndermedi; gerçekleşme anı = alınma anı'>
            damgasız
          </Badge>
        ) : (
          formatUtcDateTime(event.receivedAtUtc)
        )}
      </td>
    </tr>
  );
}

/** `datetime-local` girdisi (yerel saat) → sunucunun beklediği UTC ISO. Boşsa filtre yok. */
function localToUtcIso(local: string): string | null {
  if (!local) return null;

  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}
