import { XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EdgeRouting, EdgeRoutingLabels, LineStyle, LineStyleLabels, WireType, WireTypeLabels } from '@/models/enums';
import type { DiagramConnectionDto, PointDto } from '@/models/diagram';
import { removeWaypoint } from '@/lib/diagram/waypoints';
import { DeleteButton } from './confirm-delete';
import { ColorInput, EnumSelect, Field, NumberInput, ReadOnly, TextInput } from './panel-fields';

/**
 * Seçili kablonun paneli. Her alan commit'te (blur / seçim) ANINDA yazılır (`PUT …/connections/{id}`): alanlar
 * birbirinden bağımsız, kaydetme düğmesine gerek yok. `onConnect` yeni kabloyu nötr bir varsayılanla (gri, düz, Sinyal)
 * doğuruyor — panel olmasaydı çizilen hiçbir kablonun tipi belirlenemezdi.
 */

const WIRE_TYPES: WireType[] = [WireType.Power, WireType.Signal, WireType.DataRS485, WireType.DataEthernet, WireType.Relay, WireType.Sensor];
const LINE_STYLES: LineStyle[] = [LineStyle.Solid, LineStyle.Dashed, LineStyle.Dotted];
const ROUTINGS: EdgeRouting[] = [EdgeRouting.Orthogonal, EdgeRouting.Straight, EdgeRouting.Curved];

export function ConnectionForm({
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
              yapılır, yoksa tek bir renk seçimi onlarca istek üretirdi. */}
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
            `PUT …/connections/{id}` sözleşmesi de uç taşımıyor. */}
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
