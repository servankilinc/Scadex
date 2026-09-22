import type { SignalInnerDoorLiveDto } from '../../models/virtual-cabinet';
import { IndoorFigure } from './indoor-figure';

/**
 * İç kapıların yerleşimi. Asset'teki üç sabit kutu yerine kapı SAYISINA göre hesaplanır: bir dış kapının ardında kaç iç
 * kapı olacağı yapılandırmaya bağlıdır, üçe sabitlenmiş bir tuval dördüncü kapıyı çizemezdi.
 *
 * Izgara kabinin iç yüzeyinin alt bölümüne sığdırılır (üst bant LED ve sirenin). Sütun sayısı kapı sayısından türer,
 * sığmıyorsa tamamı ölçeklenir — kapı gövdesi hiçbir zaman kırpılmaz.
 */
interface IndoorGridProps {
  doors: SignalInnerDoorLiveDto[];
  /** Kapının anlık anahtar durumu `door.isOpen`'dadır: komut diyaloğu "kapı açıkken kilitleme" uyarısını buna bakarak gösterir. */
  onSelect: (door: SignalInnerDoorLiveDto) => void;
}

/** Kabinin iç yüzeyinde iç kapılara ayrılan alan (x 200–600, y 100–700 içinden). */
const AREA = { x: 210, y: 230, width: 380, height: 460 } as const;

/** Tek kapının kapladığı yer: gövde 140x196 + altındaki ad satırı. */
const CELL = { width: 140, height: 214, gap: 24 } as const;

export function IndoorGrid({ doors, onSelect }: IndoorGridProps) {
  if (doors.length === 0) {
    return (
      <text x={AREA.x + AREA.width / 2} y={AREA.y + AREA.height / 2} textAnchor='middle' fontSize='16' fill='#CBD5E0'>
        Bu dış kapının ardında tanımlı iç kapı yok
      </text>
    );
  }

  const { cols, scale, originX, originY } = layout(doors.length);

  return (
    <>
      {doors.map((door, index) => {
        const x = originX + (index % cols) * (CELL.width + CELL.gap) * scale;
        const y = originY + Math.floor(index / cols) * (CELL.height + CELL.gap) * scale;

        return <IndoorCell key={door.id} door={door} transform={`translate(${x}, ${y}) scale(${scale})`} onSelect={onSelect} />;
      })}
    </>
  );
}

/** Tek hücre. Durumlar canlı yayınla yamalanan sorgu verisinden gelir (`useVirtualCabinetLive`). */
function IndoorCell({ door, transform, onSelect }: { door: SignalInnerDoorLiveDto; transform: string; onSelect: (door: SignalInnerDoorLiveDto) => void }) {
  return (
    <g transform={transform}>
      <IndoorFigure name={door.name} isOpen={door.isOpen} isUnlocked={door.isUnlocked} onActivate={() => onSelect(door)} />
      <text x='70' y='210' textAnchor='middle' fontSize='13' fill='#E2E8F0'>
        {door.name}
      </text>
    </g>
  );
}

/** Sütun sayısı, ölçek ve ızgaranın sol üst köşesi. Izgara alanın ortasına hizalanır. */
function layout(count: number) {
  const cols = count <= 2 ? count : count <= 4 ? 2 : 3;
  const rows = Math.ceil(count / cols);

  const rawWidth = cols * CELL.width + (cols - 1) * CELL.gap;
  const rawHeight = rows * CELL.height + (rows - 1) * CELL.gap;

  // Büyütme yok: tek kapı tuvali doldurmasın, gövde asset'teki oranında kalsın.
  const scale = Math.min(1, AREA.width / rawWidth, AREA.height / rawHeight);

  return {
    cols,
    scale,
    originX: AREA.x + (AREA.width - rawWidth * scale) / 2,
    originY: AREA.y + (AREA.height - rawHeight * scale) / 2
  };
}
