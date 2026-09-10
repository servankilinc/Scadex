import { useState } from 'react';
import { CopyIcon, EyeIcon, LockOpenIcon, XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  AnnotationShape,
  AnnotationShapeLabels,
  DeviceStatusLabels,
  DeviceTypeLabels,
  EdgeRouting,
  EdgeRoutingLabels,
  LineStyle,
  LineStyleLabels,
  WireType,
  WireTypeLabels
} from '@/models/enums';
import type { DiagramAnnotationItemDto, DiagramConnectionDto, DiagramDeviceDto, DiagramIoChannelDto, PointDto } from '@/models/diagram';
import { useAppSelector } from '@/hooks';
import type { DiagramEditor } from '@/hooks/use-diagram-editor';
import { MIN_ALIGN } from '@/lib/diagram/align';
import { ANNOTATION_FONT_SIZE_MAX, ANNOTATION_FONT_SIZE_MIN, ANNOTATION_TEXT_MAX } from '@/lib/diagram/annotation-defaults';
import { useLiveChannel, useLiveDevice } from '@/lib/diagram/live-store';
import { removeWaypoint } from '@/lib/diagram/waypoints';
import type { DiagramNode } from '@/lib/diagram/to-rf-nodes';
import { cn } from '@/lib/utils';
import { AlignToolbar } from './align-toolbar';
import { CommandHistory } from './command-history';
import { DeleteButton } from './confirm-delete';

/**
 * Seçili öğenin, canvas üzerinde düzenlenemeyen alanları — ve seçime uygulanan
 * eylemler.
 *
 * Canvas konumu ve bağlantıyı yönetir; ad, dış kod, kablo tipi ve rengi gibi
 * alanların başka bir yeri yok. Kablo için bu kritik: `onConnect` yeni kabloyu
 * nötr bir varsayılanla (gri, düz, Sinyal) doğuruyor — panel olmasaydı çizilen
 * hiçbir kablonun tipi belirlenemezdi.
 *
 * **Her eylemin görünür bir karşılığı var.** Silme, kopyalama, hizalama ve
 * konum girişi buradan yapılabiliyor; klavye hiçbirinin TEK yolu değil. Bu
 * bilinçli bir karar — arayüzden yönetilebilen bir editör, ezberlenmesi gereken
 * bir kısayol listesinden iyidir.
 *
 * Seçim Redux'ta durur (`diagramSlice`), veri React Flow state'inde. Panel
 * ikisini birleştirir; yazma her zaman `editor` üzerinden gider ki değişiklik
 * günlüğe düşsün.
 */
export function PropertiesPanel({ editor }: { editor: DiagramEditor }) {
  const selectedNodeIds = useAppSelector(s => s.diagram.selectedNodeIds);
  const selectedEdgeIds = useAppSelector(s => s.diagram.selectedEdgeIds);

  const total = selectedNodeIds.length + selectedEdgeIds.length;

  return (
    <aside className='bg-sidebar flex w-72 shrink-0 flex-col border-l'>
      <header className='border-b px-3 py-2'>
        <h2 className='text-sm font-semibold'>Özellikler</h2>
      </header>

      <ScrollArea className='flex-1'>
        <div className='p-3'>{renderBody()}</div>
      </ScrollArea>
    </aside>
  );

  function renderBody() {
    if (total === 0) {
      return (
        <>
          <p className='text-muted-foreground text-xs'>Bir cihaz, kablo veya not seçin.</p>
          <UnreachableItems editor={editor} />
        </>
      );
    }

    // Çoklu seçimde alan düzenlemesi YOK: "20 cihazın adını birden değiştir"in
    // anlamlı bir karşılığı yok. Toplu olarak anlamlı olan iki şey — hizalama ve
    // silme — burada.
    if (total > 1) {
      return <MultiSelectionForm editor={editor} nodeIds={selectedNodeIds} edgeIds={selectedEdgeIds} />;
    }

    const edgeId = selectedEdgeIds[0];
    if (edgeId) {
      const edge = editor.edges.find(e => e.id === edgeId);
      if (!edge?.data) return null;
      return (
        <ConnectionForm
          key={edge.id}
          connection={edge.data.connection}
          onChange={patch => editor.updateConnection(edge.id, patch)}
          onDelete={() => editor.deleteElements([], [edge.id])}
        />
      );
    }

    const nodeId = selectedNodeIds[0];
    const node = editor.nodes.find(n => n.id === nodeId);
    if (!node) return null;

    if (node.type === 'annotation') return <AnnotationForm key={node.id} node={node} editor={editor} />;

    return <DeviceForm key={node.id} node={node} editor={editor} />;
  }
}

// ──────────────────────────────────────────────────────────── çoklu seçim

function MultiSelectionForm({ editor, nodeIds, edgeIds }: { editor: DiagramEditor; nodeIds: string[]; edgeIds: string[] }) {
  const selectedNodes = editor.nodes.filter(node => nodeIds.includes(node.id));
  const total = nodeIds.length + edgeIds.length;

  return (
    <div className='flex flex-col gap-3'>
      <p className='text-muted-foreground text-xs'>
        {total} öğe seçili.
        {nodeIds.length > 1 && ' Sürükleyerek hepsini birlikte taşıyabilirsiniz.'}
      </p>

      {selectedNodes.length >= MIN_ALIGN && (
        <div className='border-t pt-3'>
          <AlignToolbar nodes={selectedNodes} onMove={editor.moveNodes} />
        </div>
      )}

      <div className='border-t pt-3'>
        <DeleteButton
          label={`${total} öğeyi sil`}
          title='Seçilenleri sil'
          summary={<>{describeSelection(nodeIds.length, edgeIds.length)} silinecek. Cihazlara bağlı kablolar da silinir ve geri alınamaz.</>}
          onConfirm={() => editor.deleteElements(nodeIds, edgeIds)}
        />
      </div>
    </div>
  );
}

/** "3 cihaz ve 2 kablo" — sayılar 0 ise o parça hiç yazılmaz. */
function describeSelection(nodeCount: number, edgeCount: number): string {
  const parts: string[] = [];
  if (nodeCount > 0) parts.push(`${nodeCount} öğe`);
  if (edgeCount > 0) parts.push(`${edgeCount} kablo`);
  return parts.join(' ve ');
}

// ───────────────────────────────────────────────────────────────── cihaz

/**
 * Konum alanlarının sınırı. Sunucuda koordinat için bir kısıt YOK (`double`,
 * doğrulayıcıda aralık kuralı yok); bu yalnızca yazım hatasına karşı bir arayüz
 * korkuluğu — kaydırılmış bir tuşla cihazı canvas'ın milyonlarca piksel ötesine
 * göndermek, onu geri bulmayı imkânsız kılardı.
 */
const COORDINATE_LIMIT = 100_000;

function DeviceForm({ node, editor }: { node: Extract<DiagramNode, { type: 'template' }>; editor: DiagramEditor }) {
  const { device } = node.data;
  // Canlı durum sunucu anlık görüntüsünü ezer — bkz. `template-node.tsx`.
  const live = useLiveDevice(device.id);
  const statusId = live ? live.statusId : device.deviceStatusId;
  const lastSeen = live ? live.lastSeen : device.lastSeen;

  const onChange = (patch: Partial<DiagramDeviceDto>) => editor.updateDevice(node.id, patch);

  return (
    <div className='flex flex-col gap-3'>
      <Field label='Ad' htmlFor='device-name'>
        <TextInput id='device-name' value={device.name} onCommit={value => value && onChange({ name: value })} />
      </Field>

      <Field label='Dış kod' htmlFor='device-external-code' hint='SCADA bu kodla cihazı tanır.'>
        {/* Boş girdi null'a çevrilir: boş dize benzersizlik index'inde ikinci bir
            boş kodla çakışırdı, null'lar ise çakışmaz. */}
        <TextInput id='device-external-code' value={device.externalCode ?? ''} onCommit={value => onChange({ externalCode: value || null })} />
      </Field>

      <Field label='Dönüş (°)' htmlFor='device-rotation'>
        <NumberInput id='device-rotation' value={device.rotation} min={0} max={359} onCommit={rotation => onChange({ rotation })} />
      </Field>

      {/* Konum artık SALT OKUNUR DEĞİL. Ok tuşlarıyla itmenin (nudge) arayüz
          karşılığı bu: kutuyu piksel piksel dürtmek yerine hedef koordinatı
          doğrudan yazmak hem klavye gerektirmiyor hem daha kesin. */}
      <div className='grid grid-cols-2 gap-2'>
        <Field label='X' htmlFor='device-x'>
          <NumberInput
            id='device-x'
            value={Math.round(node.position.x)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={x => editor.moveNodes({ [node.id]: { x, y: node.position.y } })}
          />
        </Field>
        <Field label='Y' htmlFor='device-y'>
          <NumberInput
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
        <ReadOnly term='Boyut' value={`${device.template.width} × ${device.template.height}`} />
        {/* Sıfır artık "henüz üretilmedi" demek DEĞİL: pinler cihaz bırakılır
            bırakılmaz doğuyor, dolayısıyla sıfır gerçekten pinsiz bir şablondur. */}
        <ReadOnly term='Pin' value={String(device.pins.length)} />
        {/* null = hiç telemetri alınmadı; Offline ile AYNI ŞEY DEĞİL. */}
        <ReadOnly term='Durum' value={statusId == null ? 'Telemetri yok' : DeviceStatusLabels[statusId]} />
        {lastSeen && <ReadOnly term='Son görülme' value={new Date(lastSeen).toLocaleTimeString('tr-TR')} />}
      </dl>

      <LayerControls
        idPrefix='device'
        zIndex={device.zIndex}
        isLocked={device.isLocked}
        isVisible={device.isVisible}
        onChange={patch => onChange(patch)}
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

      {/* Kumanda GÖNDERME burada değil, canvas'ta sağ tıkta: komut cihaza
          yapılan bir eylemdir ve hedefin görsel olarak seçili olması gerekir.
          Panelde duran şey sonucu — geçmiş. */}
      <CommandHistory deviceId={device.id} />
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

// ───────────────────────────────────────────────────────────────── kablo

const WIRE_TYPES: WireType[] = [WireType.Power, WireType.Signal, WireType.DataRS485, WireType.DataEthernet, WireType.Relay, WireType.Sensor];
const LINE_STYLES: LineStyle[] = [LineStyle.Solid, LineStyle.Dashed, LineStyle.Dotted];
const ROUTINGS: EdgeRouting[] = [EdgeRouting.Orthogonal, EdgeRouting.Straight, EdgeRouting.Curved];

function ConnectionForm({
  connection,
  onChange,
  onDelete
}: {
  connection: DiagramConnectionDto;
  onChange: (patch: Partial<DiagramConnectionDto>) => void;
  onDelete: () => void;
}) {
  return (
    <div className='flex flex-col gap-3'>
      <Field label='Etiket' htmlFor='wire-label'>
        <TextInput id='wire-label' value={connection.label ?? ''} onCommit={value => onChange({ label: value || null })} />
      </Field>

      <Field label='Kablo tipi'>
        <EnumSelect value={connection.wireType} options={WIRE_TYPES} labels={WireTypeLabels} onChange={wireType => onChange({ wireType })} />
      </Field>

      <Field label='Renk' htmlFor='wire-color'>
        <div className='flex items-center gap-2'>
          {/* Renk seçici `onChange`'i sürükleme boyunca ateşler; commit `onBlur`'de
              yapılır, yoksa tek bir renk seçimi onlarca günlük kaydı üretirdi. */}
          <ColorInput id='wire-color' value={connection.color} onCommit={color => onChange({ color })} />
          <span className='text-muted-foreground font-mono text-xs'>{connection.color}</span>
        </div>
      </Field>

      <Field label='Çizgi stili'>
        <EnumSelect value={connection.lineStyle} options={LINE_STYLES} labels={LineStyleLabels} onChange={lineStyle => onChange({ lineStyle })} />
      </Field>

      <Field label='Kalınlık' htmlFor='wire-width'>
        <NumberInput id='wire-width' value={connection.strokeWidth} min={0.5} max={10} step={0.5} onCommit={strokeWidth => onChange({ strokeWidth })} />
      </Field>

      <Field label='Çizim'>
        <EnumSelect value={connection.routing} options={ROUTINGS} labels={EdgeRoutingLabels} onChange={routing => onChange({ routing })} />
      </Field>

      <WaypointList waypoints={connection.waypoints} onChange={waypoints => onChange({ waypoints })} />

      <dl className='text-muted-foreground grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 border-t pt-3 text-xs'>
        {/* Uçlar burada DEĞİŞTİRİLEMEZ: ucu değiştirmek yeni bir bağlantıdır,
            gerilim / yön doğrulamalarının tamamı baştan çalışmalıdır. Sunucunun
            `connections.updated` sözleşmesi de uç taşımıyor. */}
        <ReadOnly term='Uçlar' value='değiştirmek için silip yeniden çizin' />
      </dl>

      <div className='border-t pt-3'>
        {/* Kablo silmenin başka yolu yoktu: kablonun sağ tık menüsü yok ve
            `Delete` tuşu klavye gerektiriyor. Uç değiştirmenin tek yolu da
            "silip yeniden çiz" olduğu için bu düğme o akışın parçası. */}
        <DeleteButton
          label='Kabloyu sil'
          title='Kabloyu sil'
          summary={<>{connection.label ? <span className='font-medium'>{connection.label}</span> : 'Seçili kablo'} silinecek ve geri alınamaz.</>}
          onConfirm={onDelete}
        />
      </div>
    </div>
  );
}

/**
 * Kırılma noktalarının listesi.
 *
 * Canvas'ta bir kırılma noktası sürüklenerek taşınıyor ve çift tıkla siliniyor;
 * ikisi de fare jesti. Bu liste aynı işlerin GÖRÜNÜR karşılığı — çift tıklamayı
 * bilmeyen kullanıcı için silmenin bulunabilir bir yolu olması gerekiyordu.
 */
function WaypointList({ waypoints, onChange }: { waypoints: PointDto[]; onChange: (waypoints: PointDto[]) => void }) {
  return (
    <div className='flex flex-col gap-1.5 border-t pt-3'>
      <div className='flex items-center justify-between gap-2'>
        <p className='text-muted-foreground text-xs font-medium'>Kırılma noktaları</p>
        {waypoints.length > 0 && (
          <Button size='xs' variant='ghost' onClick={() => onChange([])}>
            Tümünü temizle
          </Button>
        )}
      </div>

      {waypoints.length === 0 ? (
        // Nasıl ekleneceğini SÖYLEMEK gerekiyor: artı işaretleri yalnızca kablo
        // seçiliyken çiziliyor ve seçili olmayan bir kabloda hiçbir ipucu yok.
        <p className='text-muted-foreground text-[10px]'>Yok. Canvas'ta kablonun üzerindeki + işaretine basarak ekleyin.</p>
      ) : (
        <ul className='flex flex-col gap-0.5'>
          {waypoints.map((point, index) => (
            <li key={index} className='flex items-center justify-between gap-2 text-xs'>
              <span className='text-muted-foreground font-mono'>
                {index + 1}. {Math.round(point.x)}, {Math.round(point.y)}
              </span>
              <Button
                size='xs'
                variant='ghost'
                aria-label={`${index + 1}. kırılma noktasını sil`}
                title='Bu noktayı sil'
                onClick={() => onChange(removeWaypoint(waypoints, index))}>
                <XIcon />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ───────────────────────────────────────────────────────────────── not

const SHAPES: AnnotationShape[] = [AnnotationShape.Text, AnnotationShape.Rectangle, AnnotationShape.Note, AnnotationShape.Arrow];

function AnnotationForm({ node, editor }: { node: Extract<DiagramNode, { type: 'annotation' }>; editor: DiagramEditor }) {
  const { annotation } = node.data;
  const onChange = (patch: Partial<DiagramAnnotationItemDto>) => editor.updateAnnotation(node.id, patch);

  const isArrow = annotation.shape === AnnotationShape.Arrow;
  // `Text` şeklinde arka plan ve kenarlık RENDER'da yok sayılıyor; alanları
  // göstermek, değiştirdiğinde hiçbir şey olmayan bir kontrol sunmak olurdu.
  const hasBackground = annotation.shape === AnnotationShape.Rectangle || annotation.shape === AnnotationShape.Note;
  const hasBorder = hasBackground || isArrow;

  return (
    <div className='flex flex-col gap-3'>
      <Field label='Ad' htmlFor='annotation-name' hint='Şemada görünmez; listede tanımak için.'>
        {/* Boş ad sunucuda 400 döner — boş bırakılırsa eski değer korunuyor. */}
        <TextInput id='annotation-name' value={annotation.name} onCommit={value => value && onChange({ name: value })} />
      </Field>

      <Field label={isArrow ? 'Etiket' : 'Metin'} htmlFor='annotation-text' hint={isArrow ? 'Okun üstünde görünür. Boş bırakılabilir.' : undefined}>
        <Textarea
          id='annotation-text'
          className='min-h-16 text-xs'
          maxLength={ANNOTATION_TEXT_MAX}
          defaultValue={annotation.text}
          // Commit `blur`'de: her tuş vuruşunda yazmak, her harfi ayrı bir
          // düzenleme yapar ve günlüğü şişirirdi (bkz. `TextInput`).
          onBlur={event => event.target.value !== annotation.text && onChange({ text: event.target.value })}
        />
      </Field>

      <Field label='Şekil'>
        <EnumSelect value={annotation.shape} options={SHAPES} labels={AnnotationShapeLabels} onChange={shape => onChange({ shape })} />
      </Field>

      <div className='grid grid-cols-2 gap-2'>
        <Field label='Genişlik' htmlFor='annotation-width' hint={isArrow ? 'Okun uzunluğu' : undefined}>
          {/* Sıfır genişlik/yükseklik: node canvas'ta görünmez olur ve kullanıcı
              onu bir daha seçip düzeltemez. Sunucu da bu yüzden reddediyor. */}
          <NumberInput id='annotation-width' value={annotation.width} min={1} max={COORDINATE_LIMIT} onCommit={width => onChange({ width })} />
        </Field>
        <Field label='Yükseklik' htmlFor='annotation-height' hint={isArrow ? 'Okun kalınlığı' : undefined}>
          <NumberInput id='annotation-height' value={annotation.height} min={1} max={COORDINATE_LIMIT} onCommit={height => onChange({ height })} />
        </Field>
      </div>

      <div className='grid grid-cols-2 gap-2'>
        <Field label='X' htmlFor='annotation-x'>
          <NumberInput
            id='annotation-x'
            value={Math.round(node.position.x)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={x => editor.moveNodes({ [node.id]: { x, y: node.position.y } })}
          />
        </Field>
        <Field label='Y' htmlFor='annotation-y'>
          <NumberInput
            id='annotation-y'
            value={Math.round(node.position.y)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={y => editor.moveNodes({ [node.id]: { x: node.position.x, y } })}
          />
        </Field>
      </div>

      <Field label='Dönüş (°)' htmlFor='annotation-rotation' hint={isArrow ? 'Okun yönü.' : undefined}>
        <NumberInput id='annotation-rotation' value={annotation.rotation} min={0} max={359} onCommit={rotation => onChange({ rotation })} />
      </Field>

      <div className='flex flex-col gap-3 border-t pt-3'>
        <div className='grid grid-cols-2 gap-2'>
          <Field label='Yazı boyutu' htmlFor='annotation-font-size'>
            <NumberInput
              id='annotation-font-size'
              value={annotation.fontSize}
              min={ANNOTATION_FONT_SIZE_MIN}
              max={ANNOTATION_FONT_SIZE_MAX}
              onCommit={fontSize => onChange({ fontSize })}
            />
          </Field>
          <div className='flex items-end justify-between gap-2 pb-1'>
            <Label htmlFor='annotation-bold' className='text-xs'>
              Kalın
            </Label>
            <Switch id='annotation-bold' checked={annotation.isBold} onCheckedChange={isBold => onChange({ isBold })} />
          </div>
        </div>

        <ColorField label='Yazı rengi' id='annotation-font-color' value={annotation.fontColor} onCommit={fontColor => onChange({ fontColor })} />
        {hasBackground && (
          <ColorField
            label='Arka plan'
            id='annotation-bg-color'
            value={annotation.backgroundColor}
            onCommit={backgroundColor => onChange({ backgroundColor })}
          />
        )}
        {hasBorder && (
          <ColorField
            label={isArrow ? 'Ok rengi' : 'Kenarlık'}
            id='annotation-border-color'
            value={annotation.borderColor}
            onCommit={borderColor => onChange({ borderColor })}
          />
        )}
      </div>

      <LayerControls
        idPrefix='annotation'
        zIndex={annotation.zIndex}
        isLocked={annotation.isLocked}
        isVisible={annotation.isVisible}
        onChange={patch => onChange(patch)}
      />

      <div className='border-t pt-3'>
        <DeleteButton
          label='Notu sil'
          title='Notu sil'
          summary={
            <>
              <span className='font-medium'>{annotation.name}</span> silinecek ve geri alınamaz.
            </>
          }
          onConfirm={() => editor.deleteElements([node.id], [])}
        />
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────── katman

/**
 * Katman, kilit ve görünürlük — cihazlar ve notlar için AYNI kontrol seti.
 *
 * Üç alan da her iki DTO'da var ve anlamları birebir aynı; ikisine ayrı arayüz
 * yazmak, birinde düzeltilen bir davranışın diğerinde eski kalması demek olurdu.
 */
function LayerControls({
  idPrefix,
  zIndex,
  isLocked,
  isVisible,
  onChange
}: {
  idPrefix: string;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  onChange: (patch: { zIndex?: number; isLocked?: boolean; isVisible?: boolean }) => void;
}) {
  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <Field label='Katman' htmlFor={`${idPrefix}-z`} hint='Büyük sayı üstte durur.'>
        <NumberInput id={`${idPrefix}-z`} value={zIndex} min={-999} max={999} onCommit={next => onChange({ zIndex: next })} />
      </Field>

      <div className='flex items-center justify-between gap-2'>
        <Label htmlFor={`${idPrefix}-locked`} className='text-xs'>
          Kilitli
        </Label>
        <Switch id={`${idPrefix}-locked`} checked={isLocked} onCheckedChange={locked => onChange({ isLocked: locked })} />
      </div>

      <div className='flex items-center justify-between gap-2'>
        <Label htmlFor={`${idPrefix}-visible`} className='text-xs'>
          Görünür
        </Label>
        <Switch id={`${idPrefix}-visible`} checked={isVisible} onCheckedChange={visible => onChange({ isVisible: visible })} />
      </div>

      {/* Gizlenen (ve kilitli notlarda: kilitlenen) öğe canvas'ta seçilemez,
          dolayısıyla bu panele bir daha getirilemez. Kullanıcının kendini
          kilitlediği bir duruma düşmemesi için çıkış yolu seçim boşken
          listeleniyor — bkz. `UnreachableItems`. */}
      {!isVisible && <p className='text-muted-foreground text-[10px]'>Gizli öğeler canvas'ta seçilemez; seçimi boşaltıp panelden geri getirebilirsiniz.</p>}
    </div>
  );
}

/**
 * Canvas'tan seçilemez hâle gelmiş öğeler — gizlemenin ve kilitlemenin çıkış yolu.
 *
 * Bu liste olmadan ikisi de TEK YÖNLÜ kapı olurdu:
 *
 * - **Gizli** node RF'te `hidden` olur, tıklanamaz, panele bir daha getirilemez.
 * - **Kilitli NOT** ayrıca `selectable: false` alıyor (bkz. `to-rf-nodes.ts`) —
 *   bu bilinçli: arka planda duran büyük bir çerçeve kilitlendiğinde tıklamalar
 *   altındaki cihaza geçmeli. Ama yan etkisi, o notu bir daha seçememek.
 *
 * Cihazlar kilitliyken seçilebilir kalıyor (yalnızca sürüklenemiyorlar), o
 * yüzden burada yalnızca gizli olanları listeleniyorlar.
 */
interface UnreachableItem {
  id: string;
  name: string;
  reason: 'gizli' | 'kilitli';
  kind: 'annotation' | 'device';
}

function UnreachableItems({ editor }: { editor: DiagramEditor }) {
  // Geri dönüş tipi AÇIKÇA yazılıyor: yazılmazsa çıkarım ilk dalın literal
  // tipine kilitleniyor ve "kilitli" dalı ona uymuyor.
  const items: UnreachableItem[] = editor.nodes.flatMap((node): UnreachableItem[] => {
    if (node.type === 'annotation') {
      const { annotation } = node.data;
      if (!annotation.isVisible) return [{ id: node.id, name: annotation.name, reason: 'gizli', kind: 'annotation' }];
      if (annotation.isLocked) return [{ id: node.id, name: annotation.name, reason: 'kilitli', kind: 'annotation' }];
      return [];
    }
    const { device } = node.data;
    return device.isVisible ? [] : [{ id: node.id, name: device.name, reason: 'gizli', kind: 'device' }];
  });

  if (items.length === 0) return null;

  return (
    <div className='mt-3 flex flex-col gap-1.5 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>Seçilemeyen öğeler ({items.length})</p>
      <p className='text-muted-foreground text-[10px]'>Gizli ve kilitli öğeler canvas'ta tıklanamaz; buradan geri getirin.</p>

      {items.map(item => (
        <div key={item.id} className='flex items-center justify-between gap-2 text-xs'>
          <span className='truncate'>
            {item.name} <span className='text-muted-foreground'>· {item.reason}</span>
          </span>
          <Button
            size='xs'
            variant='outline'
            onClick={() => {
              const patch = item.reason === 'gizli' ? { isVisible: true } : { isLocked: false };
              if (item.kind === 'annotation') editor.updateAnnotation(item.id, patch);
              else editor.updateDevice(item.id, patch);
            }}>
            {item.reason === 'gizli' ? <EyeIcon /> : <LockOpenIcon />}
            {item.reason === 'gizli' ? 'Göster' : 'Kilidi aç'}
          </Button>
        </div>
      ))}
    </div>
  );
}

function ColorField({ label, id, value, onCommit }: { label: string; id: string; value: string; onCommit: (value: string) => void }) {
  return (
    <div className='flex items-center justify-between gap-2'>
      <Label htmlFor={id} className='text-xs'>
        {label}
      </Label>
      <div className='flex items-center gap-2'>
        <ColorInput id={id} value={value} onCommit={onCommit} />
        <span className='text-muted-foreground font-mono text-xs'>{value}</span>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────────────────── ortak parçalar

function Field({ label, htmlFor, hint, children }: { label: string; htmlFor?: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className='flex flex-col gap-1.5'>
      <Label htmlFor={htmlFor} className='text-xs'>
        {label}
      </Label>
      {children}
      {hint && <p className='text-muted-foreground text-[10px]'>{hint}</p>}
    </div>
  );
}

function ReadOnly({ term, value }: { term: string; value: string }) {
  return (
    <>
      <dt>{term}</dt>
      <dd className='truncate text-right' title={value}>
        {value}
      </dd>
    </>
  );
}

/**
 * Metin girdisi — commit `blur` ve `Enter`'da.
 *
 * Her tuş vuruşunda `onChange` çağırmak, her harfi ayrı bir düzenleme yapardı:
 * günlük her vuruşta büyür ve ara hâller ("Pan", "Pano", "Pano-") kullanıcı için
 * anlamsızdır.
 *
 * Çağıran taraf `key={id}` verir: seçim değişince bileşen remount olur ve taslak
 * taze değerle doğar. Bunu bir effect'le yapmak fazladan bir render turu üretirdi.
 */
function TextInput({ id, value, onCommit }: { id: string; value: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value);

  const commit = () => {
    const next = draft.trim();
    if (next !== value) onCommit(next);
  };

  return (
    <Input
      id={id}
      className='h-7'
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => {
        if (e.key === 'Enter') e.currentTarget.blur();
        // Escape yazılanı atar: yanlış girilen bir dış kodu geri almanın en hızlı
        // yolu. Blur commit'i tetiklemesin diye taslak önce geri alınır.
        if (e.key === 'Escape') {
          setDraft(value);
          e.currentTarget.blur();
        }
      }}
    />
  );
}

function NumberInput({
  id,
  value,
  min,
  max,
  step,
  onCommit
}: {
  id: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  onCommit: (value: number) => void;
}) {
  const [draft, setDraft] = useState(String(value));

  const commit = () => {
    const parsed = Number(draft);
    // Boş ya da sayı olmayan girdi sessizce eski değere döner: 400 yiyecek bir
    // gövde göndermektense hiç göndermemek doğru.
    if (!Number.isFinite(parsed) || draft.trim() === '') {
      setDraft(String(value));
      return;
    }
    const clamped = Math.min(max, Math.max(min, parsed));
    setDraft(String(clamped));
    if (clamped !== value) onCommit(clamped);
  };

  return (
    <Input
      id={id}
      type='number'
      className='h-7'
      min={min}
      max={max}
      step={step}
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => e.key === 'Enter' && e.currentTarget.blur()}
    />
  );
}

function ColorInput({ id, value, onCommit }: { id: string; value: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value);

  return (
    <Input
      id={id}
      type='color'
      className='h-7 w-14 p-1'
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={() => draft !== value && onCommit(draft)}
    />
  );
}

/**
 * Sayısal enum seçici.
 *
 * Enum'lar tel üzerinde SAYI olarak gidiyor (bkz. `entityEnums.ts`); Base UI
 * `Select` değeri olduğu gibi taşıdığı için dönüştürme gerekmiyor. `SelectValue`
 * etiketi kendisi bulamaz — seçili değerin Türkçe karşılığı çocuk olarak verilir.
 */
function EnumSelect<T extends number>({
  value,
  options,
  labels,
  onChange
}: {
  value: T;
  options: T[];
  labels: Record<T, string>;
  onChange: (value: T) => void;
}) {
  return (
    <Select
      value={value}
      onValueChange={next => {
        if (next != null) onChange(next as T);
      }}>
      <SelectTrigger size='sm' className='w-full'>
        <SelectValue>{labels[value]}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map(option => (
          <SelectItem key={option} value={option}>
            {labels[option]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
