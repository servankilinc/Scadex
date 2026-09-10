import { createContext, useContext } from 'react';
import type { PointDto } from '@/models/diagram';

/**
 * Canvas items (node ve edge bileşenleri) ihtiyaç duyduğu, `data`'ya sığmayan her şey.
 *
 * React Flow node ve edge bileşenlerine yalnızca `data` ulaşır ve
 * `nodeTypes`/`edgeTypes` modül seviyesinde sabittir (değişirse RF hepsini
 * yeniden mount eder). Kabinin SCADA durumunu her cihazın `data`'sına kopyalamak,
 * aynı bilginin node sayısı kadar tekrarı ve her kabin ayarı değişiminde tüm
 * node'ların yeniden üretilmesi olurdu. Düzenleme geri çağrıları için de aynı
 * gerekçe geçerli.
 *
 * **Geri çağrılar KİMLİĞİ SABİT olmak zorunda.** `useDiagramEditor` onları
 * `useCallback` ile üretiyor; her render'da yeni bir fonksiyon gelseydi context
 * değeri değişir ve bir tuşa basmak bile grafın tamamını yeniden çizdirirdi.
 */
export interface DiagramCanvasContextValue {
  cabinetId: string;
  /** False ise kumanda ucu 400 döner; menü bunu ÖNCEDEN söyler. */
  scadaIsEnabled: boolean;
  /**
   * Cihazı ve kablolarını siler.
   *
   * Menüde durmasının sebebi klavyeyle ilgili: `Delete` tuşu tek yol olmamalı.
   */
  onDelete: (deviceId: string) => void;
  onDuplicate: (deviceId: string) => void;
  /** Kablonun kırılma noktalarını yazar. Sürükleme bitince BİR kez çağrılır. */
  onWaypointsChange: (connectionId: string, waypoints: PointDto[]) => void;
}

const DiagramCanvasContext = createContext<DiagramCanvasContextValue | null>(null);

export const DiagramCanvasProvider = DiagramCanvasContext.Provider;

/**
 * Sağlayıcı yoksa `null` döner ve düzenleme arayüzü hiç çizilmez. Bileşenler
 * editör dışında (ör. bir önizlemede) render edilirse sessizce çalışmaya devam eder.
 */
export function useDiagramCanvasContext(): DiagramCanvasContextValue | null {
  return useContext(DiagramCanvasContext);
}
