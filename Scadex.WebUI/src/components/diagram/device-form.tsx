import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { CopyIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { CameraBrand, CameraBrandLabels, DeviceType, DeviceTypeLabels, deviceStatusLabel } from '@/models/enums';
import type { DiagramCameraDto, DiagramControlCardDto, DiagramIoChannelDto, DiagramPcDeviceDto } from '@/models/diagram';
import type { DiagramEditor } from '@/hooks/use-diagram-editor';
import { generateDeviceSecret, normalizeMacAddress } from '@/lib/diagram/device-credential';
import { pickDeviceProperties, savedDeviceProperties, type DeviceProperties } from '@/lib/diagram/diagram-requests';
import { useLiveChannel, useLiveDevice } from '@/lib/diagram/live-store';
import { deviceSize, type DeviceNode } from '@/lib/diagram/to-rf-nodes';
import { cn, formatUtcDateTime } from '@/lib/utils';
import { CommandHistory } from './command-history';
import { DeleteButton } from './confirm-delete';
import { COORDINATE_LIMIT, EnumSelect, Field, LayerControls, NumberInput, OptionalNumberInput, PasswordInput, ReadOnly, TextInput } from './panel-fields';

/**
 * Seçili cihazın paneli.
 *
 * **İki tür alan var.** Ad, IP, izleme ve tipe özgü alanlar (kamera / kontrol kartı / PC) bir TASLAKTA birikir ve
 * "Kaydet" ile tek istekte gider (`PUT …/devices/{id}`): bu alanlar birbirine bağlı (izleme IP ister, kamera IP'siz
 * olamaz) ve tek tek gönderilseler ara hâller 400 alırdı. Dönüş, boyut, konum ve katman ise tuvalle aynı yoldan ANINDA
 * yazılır (`PUT …/layout`).
 *
 * Seçim değişince taslak atılır; kaydedilmemiş değişiklik varsa uyarı gösterilir.
 */
export function DeviceForm({ node, editor, readOnly }: { node: DeviceNode; editor: DiagramEditor; readOnly: boolean }) {
  const { device } = node.data;
  // Canlı durum sunucu anlık görüntüsünü ezer — bkz. `template-node.tsx`.
  const live = useLiveDevice(device.id);
  const statusId = live ? live.statusId : device.deviceStatusId;
  const lastSeen = live ? live.lastSeen : device.lastSeen;

  const [initial, setInitial] = useState(() => pickDeviceProperties(device));
  const [draft, setDraft] = useState(initial);
  // Vazgeç'te girdiler yeniden doğsun diye: `TextInput` taslağını yalnızca mount'ta kuruyor.
  const [revision, setRevision] = useState(0);
  const [saving, setSaving] = useState(false);
  const dirty = JSON.stringify(draft) !== JSON.stringify(initial);

  const onChange = (patch: Partial<DeviceProperties>) => setDraft(current => ({ ...current, ...patch }));

  const save = async () => {
    setSaving(true);
    const ok = await editor.saveDevice(device.id, draft);
    setSaving(false);
    if (!ok) return;
    const saved = savedDeviceProperties(draft);
    setInitial(saved);
    setDraft(saved);
  };

  const discard = () => {
    setDraft(initial);
    setRevision(current => current + 1);
  };

  useDiscardWarning(dirty);

  // Girdilerde EFEKTİF ölçü gösterilir: override yokken kutu boş kalsaydı
  // kullanıcı "kaç piksel?" sorusunu ancak şablona bakarak cevaplardı.
  const size = deviceSize(device);
  const hasSizeOverride = device.width != null || device.height != null;
  const isCamera = device.template.deviceTypeId === DeviceType.Camera;

  return (
    <div className='flex flex-col gap-3'>
      <div key={revision} className='flex flex-col gap-3'>
        <Field label='Ad' htmlFor='device-name'>
          <TextInput id='device-name' value={draft.name} onCommit={value => value && onChange({ name: value })} />
        </Field>

        <Field label='IP adresi' htmlFor='device-ip-address' hint={isCamera ? 'Kamera için zorunlu: RTSP adresi bundan kurulur.' : undefined}>
          <TextInput id='device-ip-address' value={draft.ipAddress ?? ''} onCommit={value => onChange({ ipAddress: value || null })} />
        </Field>

        {/* Tipe özgü alanlar cihazın ek nesnelerindedir (2026-10-22) — kimlik bilgileri cihaz gövdesinde yok. */}
        {draft.camera && <CameraFields camera={draft.camera} onChange={camera => onChange({ camera })} />}
        {draft.controlCard && <ControlCardFields controlCard={draft.controlCard} onChange={controlCard => onChange({ controlCard })} />}
        {draft.pc && (
          <CredentialSection
            title='Bilgisayar'
            idPrefix='pc'
            deviceId={device.id}
            credential={draft.pc}
            saved={draft.pc.secret === initial.pc?.secret}
            purpose='Uzak masaüstü istemcisi bu kimlikle bağlanır: PC’deki appsettings.json > RemoteDesk:DeviceId ve DeviceSecret.'
            onChange={patch => onChange({ pc: { ...draft.pc!, ...patch } })}
          />
        )}

        {/* İzleme yalnızca izlenebilir şablonda (`template.isMonitorable`) sunulur — klemensin, sigortanın yoklanacak bir ağı
            yok. Şablon izlenemezken izleme bir şekilde açık kalmışsa bölüm yine görünür ki kapatılabilsin. */}
        {(device.template.isMonitorable || draft.isMonitoringEnabled) && (
          <MonitoringFields draft={draft} lastConnectionError={device.lastConnectionError} onChange={onChange} />
        )}
      </div>

      {!readOnly && (dirty || saving) && (
        <div className='bg-muted/60 flex items-center gap-2 rounded-md border p-2'>
          <span className='text-muted-foreground mr-auto text-xs'>Kaydedilmemiş değişiklik</span>
          <Button size='xs' variant='outline' disabled={saving} onClick={discard}>
            Vazgeç
          </Button>
          <Button size='xs' disabled={saving} onClick={() => void save()}>
            {saving ? 'Kaydediliyor…' : 'Kaydet'}
          </Button>
        </div>
      )}

      {/* Buradan sonrası ANINDA yazılır (tuvalle aynı yerleşim ucu). */}
      <Field label='Dönüş (°)' htmlFor='device-rotation'>
        <NumberInput
          id='device-rotation'
          value={device.rotation}
          min={0}
          max={359}
          onCommit={rotation => editor.updateDeviceLayout(node.id, { rotation })}
        />
      </Field>

      {/* Boyut cihaz BAZINDA override edilir; `null` bırakıldığında şablonun ölçüsü geçerlidir. En-boy oranı serbest: pinler
          kutunun 0..1 kesriyle, arka plan görseli de `object-fill` ile konumlandığı için ikisi aynı oranda esner.

          `key` ŞART: `NumberInput` taslağını yalnızca mount'ta kuruyor; bu olmasaydı "şablon boyutuna dön"e basıldığında kutu
          küçülür ama girdideki sayı eski değerde donup kalırdı. */}
      <div className='grid grid-cols-2 gap-2'>
        <Field label='Genişlik' htmlFor='device-width' hint={hasSizeOverride ? undefined : 'Şablon ölçüsü'}>
          <NumberInput
            key={`device-width-${size.width}`}
            id='device-width'
            value={Math.round(size.width)}
            min={1}
            max={COORDINATE_LIMIT}
            onCommit={width => editor.updateDeviceLayout(node.id, { width })}
          />
        </Field>
        <Field label='Yükseklik' htmlFor='device-height'>
          <NumberInput
            key={`device-height-${size.height}`}
            id='device-height'
            value={Math.round(size.height)}
            min={1}
            max={COORDINATE_LIMIT}
            onCommit={height => editor.updateDeviceLayout(node.id, { height })}
          />
        </Field>
      </div>

      {/* İki eksen BİRLİKTE sıfırlanır: "genişlik şablondan, yükseklik elle" gibi bir ara durum anlaşılmaz olurdu. */}
      {hasSizeOverride && (
        <Button size='xs' variant='outline' className='self-start' onClick={() => editor.updateDeviceLayout(node.id, { width: null, height: null })}>
          Şablon boyutuna dön
        </Button>
      )}

      {/* Ok tuşlarıyla itmenin (nudge) arayüz karşılığı: hedef koordinatı doğrudan yazmak hem klavye gerektirmiyor hem daha kesin. */}
      <div className='grid grid-cols-2 gap-2'>
        <Field label='X' htmlFor='device-x'>
          <NumberInput
            key={`device-x-${node.position.x}`}
            id='device-x'
            value={Math.round(node.position.x)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={x => editor.moveNodes({ [node.id]: { x, y: node.position.y } })}
          />
        </Field>
        <Field label='Y' htmlFor='device-y'>
          <NumberInput
            key={`device-y-${node.position.y}`}
            id='device-y'
            value={Math.round(node.position.y)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={y => editor.moveNodes({ [node.id]: { x: node.position.x, y } })}
          />
        </Field>
      </div>

      <dl className='text-muted-foreground grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 border-t pt-3 text-xs'>
        <ReadOnly term='Şablon' value={device.template.name} />
        <ReadOnly term='Tip' value={DeviceTypeLabels[device.template.deviceTypeId]} />
        {/* Override varken "geri dönersem ne olacak" sorusunun cevabı YALNIZCA burada. */}
        <ReadOnly term='Şablon boyutu' value={`${device.template.width} × ${device.template.height}`} />
        <ReadOnly term='Pin' value={String(device.pins.length)} />
        {/* null = bilinmiyor (canlılık kanıtı yok); Offline ile AYNI ŞEY DEĞİL. */}
        <ReadOnly term='Durum' value={deviceStatusLabel(statusId)} />
        {/* Sunucu damgası `Z`'siz gelir; çıplak `new Date` onu yerel saat sayıp kaydırırdı. */}
        {lastSeen && <ReadOnly term='Son görülme' value={formatUtcDateTime(lastSeen)} />}
      </dl>

      <LayerControls
        idPrefix='device'
        zIndex={device.zIndex}
        isLocked={device.isLocked}
        isVisible={device.isVisible}
        onChange={patch => editor.updateDeviceLayout(node.id, patch)}
      />

      <div className='flex flex-col gap-2 border-t pt-3'>
        <Button size='sm' variant='outline' onClick={() => editor.duplicateDevice(node.id)}>
          <CopyIcon />
          Kopyala
        </Button>
        <DeleteButton
          label='Cihazı sil'
          title='Cihazı sil'
          summary={
            <>
              <span className='font-medium'>{device.name}</span> silinecek. Bu cihaza bağlı kablolar da silinir ve geri alınamaz.
            </>
          }
          onConfirm={() => editor.deleteElements([node.id], [])}
        />
      </div>

      {device.ioChannels.length > 0 && (
        <div className='flex flex-col gap-1.5 border-t pt-3'>
          <p className='text-muted-foreground text-xs font-medium'>Kanallar</p>
          <div className='flex flex-col gap-0.5'>
            {device.ioChannels.map(channel => (
              <ChannelRow key={channel.id} channel={channel} />
            ))}
          </div>
        </div>
      )}

      {/* Kumanda GÖNDERME burada değil, canvas'ta sağ tıkta: komut cihaza yapılan bir eylemdir ve hedefin görsel olarak
          seçili olması gerekir. Panelde duran şey sonucu — geçmiş. */}
      <CommandHistory deviceId={device.id} />
    </div>
  );
}

/**
 * Form kaydedilmeden kapanırsa (başka öğe seçildi) taslak atılır — bunu sessiz yapmamak için uyarı. Engellemek yerine
 * bildirmek bilinçli: seçimi bir formun durdurması tuvalde gezinmeyi bozardı.
 */
function useDiscardWarning(dirty: boolean) {
  const dirtyRef = useRef(dirty);
  useLayoutEffect(() => {
    dirtyRef.current = dirty;
  });
  useEffect(() => {
    const state = dirtyRef;
    return () => {
      if (state.current) toast.warning('Kaydedilmemiş cihaz değişiklikleri atıldı');
    };
  }, []);
}

// Ek nesne alanları. Kurallar sunucudaki `DeviceExtensionDrafts.cs` validator'larının ELLE tutulan aynasıdır (model ≤ 64,
// açıklama ≤ 512, kullanıcı/parola ≤ 128; kart / PC sırrı yalnızca `generateDeviceSecret` biçiminde). Boş girdi `null`'a çevrilir.

const CAMERA_BRANDS: CameraBrand[] = Object.values(CameraBrand);

function CameraFields({ camera, onChange }: { camera: DiagramCameraDto; onChange: (camera: DiagramCameraDto) => void }) {
  const patch = (next: Partial<DiagramCameraDto>) => onChange({ ...camera, ...next });
  const pending = camera.password;
  const isSet = pending != null ? pending !== '' : camera.hasPassword;

  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>Kamera</p>
      <Field label='Marka'>
        <EnumSelect value={camera.brand} options={CAMERA_BRANDS} labels={CameraBrandLabels} onChange={brand => patch({ brand })} />
      </Field>
      <Field label='Model' htmlFor='camera-model'>
        <TextInput id='camera-model' value={camera.model ?? ''} maxLength={64} onCommit={value => patch({ model: value || null })} />
      </Field>
      <Field label='Açıklama' htmlFor='camera-description'>
        <TextInput id='camera-description' value={camera.description ?? ''} maxLength={512} onCommit={value => patch({ description: value || null })} />
      </Field>
      <Field label='Kullanıcı adı' htmlFor='camera-username'>
        <TextInput id='camera-username' value={camera.username ?? ''} maxLength={128} onCommit={value => patch({ username: value || null })} />
      </Field>
      {/* Sunucu parolayı DÖNDÜRMEZ (`hasPassword`); girilen değer kaydedilene kadar `password`'ta bekler. */}
      <Field
        label='Parola'
        htmlFor='camera-password'
        hint={pending ? 'Yeni parola kaydedilince geçerli olur.' : pending === '' ? 'Kaydedince parola temizlenir.' : isSet ? 'Parola ayarlı. Değiştirmek için yenisini yazın.' : 'Parola yok.'}>
        <div className='flex items-center gap-2'>
          <PasswordInput id='camera-password' onCommit={password => patch({ password })} />
          {isSet && (
            <Button size='xs' variant='ghost' onClick={() => patch({ password: '' })}>
              Temizle
            </Button>
          )}
        </div>
      </Field>
      <p className='text-muted-foreground text-[10px]'>Bilgiler kaydedilince açık izlemeler yeni bilgilerle yeniden bağlanır.</p>
    </div>
  );
}

/**
 * Kontrol kartının kimliği (2026-10-28): SCADA her istekte `DeviceId` header'ında kartın MAC adresini gönderir, kart ve kabin
 * buradan çözülür. Yazım blur'da tek tipe (`AA:BB:CC:DD:EE:FF`) çevrilir; çevrilemeyen değer olduğu gibi gider, sunucu reddeder.
 */
function ControlCardFields({ controlCard, onChange }: { controlCard: DiagramControlCardDto; onChange: (controlCard: DiagramControlCardDto) => void }) {
  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>Kontrol kartı</p>
      <Field
        label='MAC adresi'
        htmlFor='card-mac-address'
        hint={controlCard.macAddress ? 'SCADA ingest ve kart okumalarını DeviceId header’ında bu adresle gönderir.' : 'MAC adresi yok — kart bağlanamaz.'}
      >
        {/* `TextInput` taslağını yalnızca mount'ta kurar: tek tipe çevrilen yazım kutuda görünsün diye değer değişince yeniden doğar. */}
        <TextInput
          key={controlCard.macAddress ?? ''}
          id='card-mac-address'
          value={controlCard.macAddress ?? ''}
          maxLength={32}
          onCommit={value => onChange({ macAddress: value ? (normalizeMacAddress(value) ?? value) : null })}
        />
      </Field>
    </div>
  );
}

/**
 * PC'nin Basic Auth kimliği: kullanıcı adı cihazın Id'si, parola tarayıcının ürettiği sır (`generateDeviceSecret`). Sunucu sırrı
 * DÖNDÜRMEZ (`hasCredential`); üretilen sır kaydedildikten sonra da bu oturumda görünür kalır — operatör onu PC'ye şimdi girmeli.
 * Elle sır yazılmaz: sunucu yalnızca üretilen biçimi kabul eder.
 */
function CredentialSection({
  title,
  idPrefix,
  deviceId,
  credential,
  saved,
  purpose,
  onChange
}: {
  title: string;
  idPrefix: string;
  deviceId: string;
  credential: DiagramPcDeviceDto;
  /** Bekleyen sır zaten kaydedildi mi (yalnızca gösterim için tutuluyor). */
  saved: boolean;
  purpose: string;
  onChange: (patch: { secret: string }) => void;
}) {
  const pending = credential.secret;
  const isSet = pending != null ? pending !== '' : credential.hasCredential;
  const status =
    pending === ''
      ? 'Kaydedince kimlik iptal edilir; cihaz bağlanamaz.'
      : pending && saved
        ? 'Sır kaydedildi. Sayfa yenilenince bir daha gösterilmez — şimdi cihaza girin.'
        : pending
          ? 'Yeni sır kaydedilince geçerli olur, eskisi geçersizleşir.'
          : credential.hasCredential
            ? 'Kimlik bilgisi ayarlı. Sır sunucuda saklanmaz; kaybolduysa yenisini üretin.'
            : 'Kimlik bilgisi yok — cihaz bağlanamaz.';

  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>{title}</p>
      <Field label='Kullanıcı adı' htmlFor={`${idPrefix}-credential-user`} hint={purpose}>
        <Input id={`${idPrefix}-credential-user`} className='h-7 font-mono text-xs' value={deviceId} readOnly onFocus={e => e.currentTarget.select()} />
      </Field>
      <Field label='Sır (parola)' htmlFor={pending ? `${idPrefix}-credential-secret` : undefined} hint={status}>
        {pending && (
          <Input id={`${idPrefix}-credential-secret`} className='h-7 font-mono text-xs' value={pending} readOnly onFocus={e => e.currentTarget.select()} />
        )}
        <div className='flex items-center gap-2'>
          <Button size='xs' variant='outline' onClick={() => onChange({ secret: generateDeviceSecret() })}>
            {isSet ? 'Yenile' : 'Üret'}
          </Button>
          {isSet && (
            <Button size='xs' variant='ghost' onClick={() => onChange({ secret: '' })}>
              İptal et
            </Button>
          )}
        </div>
      </Field>
    </div>
  );
}

/**
 * İzleme (sunucuda `IMonitoredAsset`): açıksa cihaz `IP:port`'a TCP ile, port boşsa IP'ye ICMP ping ile yoklanır ve sonuç
 * hem cihazın hem kabinin durumuna yansır.
 *
 * Kurallar sunucudaki `DeviceUpdateRequestValidator`'ın ELLE tutulan aynasıdır: port 1..65535 (boş bırakılabilir), periyot
 * 5..86400 sn, izleme açıkken IP zorunlu. Eksik IP burada engellenmez, yalnızca işaretlenir — son söz kaydetmede sunucunundur.
 */
function MonitoringFields({
  draft,
  lastConnectionError,
  onChange
}: {
  draft: DeviceProperties;
  lastConnectionError: string | null;
  onChange: (patch: Partial<DeviceProperties>) => void;
}) {
  const missingTarget = draft.isMonitoringEnabled && !draft.ipAddress;
  const usesPing = draft.monitoringPort == null;

  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <div className='flex items-center justify-between gap-2'>
        <Label htmlFor='device-monitoring' className='text-xs'>
          İzleme
        </Label>
        <Switch id='device-monitoring' checked={draft.isMonitoringEnabled} onCheckedChange={isMonitoringEnabled => onChange({ isMonitoringEnabled })} />
      </div>

      {draft.isMonitoringEnabled && (
        <>
          <div className='grid grid-cols-2 gap-2'>
            <Field label='Port (boş = ping)' htmlFor='device-monitoring-port'>
              <OptionalNumberInput
                id='device-monitoring-port'
                value={draft.monitoringPort}
                min={1}
                max={65535}
                onCommit={monitoringPort => onChange({ monitoringPort })}
              />
            </Field>
            <Field label='Periyot (sn)' htmlFor='device-ping-interval'>
              <NumberInput id='device-ping-interval' value={draft.pingIntervalSec} min={5} max={86400} onCommit={pingIntervalSec => onChange({ pingIntervalSec })} />
            </Field>
          </div>
          {missingTarget ? (
            <p className='text-destructive text-[10px]'>İzleme için IP adresi gerekli; eksikse kaydetme reddedilir.</p>
          ) : (
            <p className='text-muted-foreground text-[10px]'>
              {usesPing
                ? "Port boş: cihaza ping (ICMP) atılır — yalnızca ağda olduğunu gösterir; ICMP kapalıysa sağlamken Çevrimdışı görünür. "
                : 'Cihaz bu porta TCP ile yoklanır. '}
              Yoklama başarısız olursa cihaz Çevrimdışı sayılır; kabin Uyarı'ya geçer (kontrol modülünde Çevrimdışı).
            </p>
          )}
        </>
      )}

      {lastConnectionError && <p className='text-destructive text-[10px] break-words'>Son hata: {lastConnectionError}</p>}
    </div>
  );
}

/**
 * Tek bir IO kanalının canlı değeri.
 *
 * AYRI bileşen: abonelik buraya ait olduğu için bir kanal değiştiğinde yalnızca
 * o satır yeniden render olur, tüm panel değil.
 */
function ChannelRow({ channel }: { channel: DiagramIoChannelDto }) {
  const live = useLiveChannel(channel.id);

  return (
    <div className='flex items-baseline justify-between gap-2 text-xs'>
      <span className='text-muted-foreground truncate'>
        <span className='font-mono'>CH{channel.channelNumber}</span> · {channel.name}
      </span>
      <span className={cn('shrink-0 font-mono', live?.value == null && 'text-muted-foreground/60')} title={live ? new Date(live.updatedAt).toLocaleString('tr-TR') : undefined}>
        {live?.value ?? '—'}
      </span>
    </div>
  );
}
