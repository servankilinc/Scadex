import { useCallback, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, useReactFlow, type EdgeProps } from '@xyflow/react';
import { useDiagramCanvasContext } from '@/lib/diagram/canvas-context';
import type { DiagramEdge } from '@/lib/diagram/to-rf-edges';
import { buildWaypointPath, insertWaypoint, moveWaypoint, removeWaypoint, segmentMidpoints, waypointPathMidpoint } from '@/lib/diagram/waypoints';
import type { PointDto } from '@/models/diagram';

/**
 * Dik açılı kablo — referans diyagramdaki hatların çizim şekli.
 *
 * İki mod:
 *   - Kayıtlı `waypoints` VARSA: kullanıcının koyduğu kırılma noktalarından
 *     birebir geçilir. Araya "akıllı" köşe uydurulmaz; draw.io'nun davranışı da
 *     budur — bir noktayı taşıdığınızda kablo tam oradan geçer.
 *   - YOKSA: React Flow'un `getSmoothStepPath`'ine düşülür. `WaypointsJson`'ın
 *     nullable olması tam olarak bunun için gerekliydi: draw-first UX'te yeni
 *     çizilen kablonun henüz kırılma noktası yoktur ve anında çizilebilmelidir.
 *
 * **Tutamaklar yalnızca kablo SEÇİLİYKEN çizilir.** Her kabloda sürekli duran
 * artı işaretleri, kalabalık bir şemayı okunmaz hale getirirdi; ayrıca seçim
 * zaten kullanıcının "bu kabloyla ilgileniyorum" beyanıdır.
 */
export function OrthogonalEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  style,
  markerEnd,
  label,
  selected,
  data
}: EdgeProps<DiagramEdge>) {
  const context = useDiagramCanvasContext();
  const { screenToFlowPosition } = useReactFlow();

  /**
   * Sürükleme sırasındaki geçici konum.
   *
   * Her `pointermove`'da `onWaypointsChange` çağrılsaydı değişiklik günlüğüne
   * saniyede onlarca yazma düşerdi. Aynı kural node konumlarında da uygulanıyor:
   * konum yalnızca sürükleme BİTTİĞİNDE kaydediliyor (`onNodeDragStop`).
   */
  const [drag, setDrag] = useState<{ index: number; point: PointDto } | null>(null);

  const saved = data?.connection.waypoints ?? [];
  const waypoints = drag ? moveWaypoint(saved, drag.index, drag.point) : saved;
  const hasWaypoints = waypoints.length > 0;

  // Uc noktalar sunucudan GELMEZ; RF'in hesapladigi handle konumlaridir.
  const source = { x: sourceX, y: sourceY };
  const target = { x: targetX, y: targetY };

  const [smoothPath, smoothLabelX, smoothLabelY] = getSmoothStepPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition
  });

  const path = hasWaypoints ? buildWaypointPath(source, waypoints, target) : smoothPath;
  const midpoint = hasWaypoints ? waypointPathMidpoint(source, waypoints, target) : { x: smoothLabelX, y: smoothLabelY };

  const toFlowPoint = useCallback(
    (event: { clientX: number; clientY: number }): PointDto => {
      const position = screenToFlowPosition({ x: event.clientX, y: event.clientY });
      // Tam sayıya yuvarlanıyor: kablo köşelerinin 312.7431 gibi bir koordinatta
      // durması ne okunabilir ne de yeniden üretilebilir.
      return { x: Math.round(position.x), y: Math.round(position.y) };
    },
    [screenToFlowPosition]
  );

  const editable = selected === true && context != null && data != null;

  return (
    <>
      <BaseEdge id={id} path={path} style={style} markerEnd={markerEnd} />

      {label != null && (
        <EdgeLabelRenderer>
          <div
            // nodrag/nopan: etiket RF'in etkilesim katmaninin USTUNDE durur,
            // bu siniflar olmadan etiketin uzerinden surukleme canvas'i kaydirir.
            className='nodrag nopan bg-background/90 text-foreground pointer-events-auto absolute rounded border px-1 py-0.5 text-[10px] shadow-sm'
            style={{ transform: `translate(-50%, -50%) translate(${midpoint.x}px, ${midpoint.y}px)` }}>
            {label}
          </div>
        </EdgeLabelRenderer>
      )}

      {editable && (
        <EdgeLabelRenderer>
          {/* Var olan kırılma noktaları: sürükle, çift tıkla sil. */}
          {waypoints.map((point, index) => (
            <div
              key={index}
              className='nodrag nopan pointer-events-auto absolute size-2.5 cursor-move rounded-full border-2 border-white bg-sky-500 shadow'
              style={{ transform: `translate(-50%, -50%) translate(${point.x}px, ${point.y}px)` }}
              title='Sürükleyerek taşıyın · silmek için çift tıklayın'
              onPointerDown={(event: ReactPointerEvent<HTMLDivElement>) => {
                // stopPropagation ŞART: aksi halde RF bu basışı canvas'ta bir
                // seçim/kaydırma başlangıcı sayar ve tutamak hiç sürüklenmez.
                event.stopPropagation();
                event.currentTarget.setPointerCapture(event.pointerId);
                setDrag({ index, point });
              }}
              onPointerMove={(event: ReactPointerEvent<HTMLDivElement>) => {
                if (drag?.index !== index) return;
                setDrag({ index, point: toFlowPoint(event) });
              }}
              onPointerUp={(event: ReactPointerEvent<HTMLDivElement>) => {
                if (drag?.index !== index) return;
                event.currentTarget.releasePointerCapture(event.pointerId);
                setDrag(null);
                context.onWaypointsChange(id, moveWaypoint(saved, index, toFlowPoint(event)));
              }}
              onDoubleClick={event => {
                event.stopPropagation();
                context.onWaypointsChange(id, removeWaypoint(saved, index));
              }}
            />
          ))}

          {/* Parça ortalarındaki "+": buraya yeni kırılma ekler. Sürüklenen
              tutamağın yanında ikinci bir hedef göstermemek için sürükleme
              sırasında hiç çizilmiyorlar. */}
          {!drag &&
            segmentMidpoints(source, saved, target).map((point, segmentIndex) => (
              <button
                key={`add-${segmentIndex}`}
                type='button'
                className='nodrag nopan text-muted-foreground hover:bg-sky-500 hover:text-white pointer-events-auto absolute grid size-4 place-items-center rounded-full border bg-white text-[11px] leading-none shadow'
                style={{ transform: `translate(-50%, -50%) translate(${point.x}px, ${point.y}px)` }}
                title='Buraya kırılma noktası ekle'
                aria-label='Kırılma noktası ekle'
                onPointerDown={event => event.stopPropagation()}
                onClick={event => {
                  event.stopPropagation();
                  context.onWaypointsChange(id, insertWaypoint(saved, segmentIndex, point));
                }}>
                +
              </button>
            ))}
        </EdgeLabelRenderer>
      )}
    </>
  );
}
