import { createPortal } from 'react-dom';
import type { SignalInnerDoorLiveDto } from '../../models/virtual-cabinet';
import { useArtwork, type ArtworkBox } from './artwork';
import { IndoorFigure } from './indoor-figure';

/**
 * İç kapıların yerleşimi — kapı SAYISINA göre hesaplanır: bir dış kapının ardında kaç iç kapı olacağı yapılandırmaya
 * bağlıdır. Görünümü Figma belirler (`Indoor-Opened.svg` / `Indoor-Closed.svg`), yerleşimi kod: ızgara kasadaki
 * `#Indoor-List-Box`'ın çerçevesine sığdırılır, sütun sayısı kapı sayısından türer, sığmıyorsa tamamı ölçeklenir — kapı
 * gövdesi hiçbir zaman kırpılmaz.
 */
interface IndoorGridProps {
  doors: SignalInnerDoorLiveDto[];
  /** Kapının anlık anahtar durumu `door.isOpen`'dadır: komut diyaloğu "kapı açıkken kilitleme" uyarısını buna bakarak gösterir. */
  onSelect: (door: SignalInnerDoorLiveDto) => void;
}

/** Hücrede gövdenin altındaki ad satırı ve hücreler arası boşluk (SVG birimi). */
const NAME_ROW_HEIGHT = 18;
const CELL_GAP = 24;

export function IndoorGrid({ doors, onSelect }: IndoorGridProps) {
  const { slots, symbolSizes } = useArtwork();
  const opened = symbolSizes['vc-indoor-open'];
  const closed = symbolSizes['vc-indoor-closed'];
  if (!slots.indoorList || !opened || !closed) return null;

  const { frame: area, layer } = slots.indoorList;

  if (doors.length === 0) {
    return createPortal(
      <text x={area.x + area.width / 2} y={area.y + area.height / 2} textAnchor='middle' fontSize='16' fill='#CBD5E0'>
        Bu dış kapının ardında tanımlı iç kapı yok
      </text>,
      layer
    );
  }

  // Açık ve kapalı gövde aynı hücreye sığmalı.
  const body = { width: Math.max(opened.width, closed.width), height: Math.max(opened.height, closed.height) };
  const cell = { width: body.width, height: body.height + NAME_ROW_HEIGHT };
  const { cols, scale, originX, originY } = layout(doors.length, area, cell);

  return createPortal(
    doors.map((door, index) => {
      const x = originX + (index % cols) * (cell.width + CELL_GAP) * scale;
      const y = originY + Math.floor(index / cols) * (cell.height + CELL_GAP) * scale;

      return (
        <g key={door.id} transform={`translate(${x}, ${y}) scale(${scale})`}>
          <IndoorFigure
            name={door.name}
            isOpen={door.isOpen}
            isUnlocked={door.isUnlocked}
            size={body}
            onActivate={() => onSelect(door)}
          />
          <text x={body.width / 2} y={body.height + NAME_ROW_HEIGHT - 4} textAnchor='middle' fontSize='13' fill='#E2E8F0'>
            {door.name}
          </text>
        </g>
      );
    }),
    layer
  );
}

/** Sütun sayısı, ölçek ve ızgaranın sol üst köşesi. Izgara alanın ortasına hizalanır. */
function layout(count: number, area: ArtworkBox, cell: { width: number; height: number }) {
  const cols = count <= 2 ? count : count <= 4 ? 2 : 3;
  const rows = Math.ceil(count / cols);

  const rawWidth = cols * cell.width + (cols - 1) * CELL_GAP;
  const rawHeight = rows * cell.height + (rows - 1) * CELL_GAP;

  // Büyütme yok: tek kapı alanı doldurmasın, gövde Figma'daki boyutunda kalsın.
  const scale = Math.min(1, area.width / rawWidth, area.height / rawHeight);

  return {
    cols,
    scale,
    originX: area.x + (area.width - rawWidth * scale) / 2,
    originY: area.y + (area.height - rawHeight * scale) / 2
  };
}
