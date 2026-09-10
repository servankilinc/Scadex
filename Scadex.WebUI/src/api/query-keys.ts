/**
 * TanStack Query anahtarları.
 */
import type { ChannelEventQueryRequest } from '@/models/channelEvent';

export const diagramKeys = {
  all: ['diagram'] as const,
  /** Bir kabinin tüm grafı — canvas ayarları DAHİL (aggregate'in içinde gelir). */
  cabinet: (cabinetId: string) => [...diagramKeys.all, 'cabinet', cabinetId] as const,
  /** Bir cihazın komut geçmişi. */
  deviceCommands: (deviceId: string) => [...diagramKeys.all, 'device', deviceId, 'commands'] as const
};

export const componentTemplateKeys = {
  all: ['component-template'] as const,
  /**
   * Palet her kabinette aynı olduğu için uzun `staleTime` ile cachelenir. Anahtar
   * diyagramın ALTINDA DEĞİL: şablon kütüphanesi kabinden bağımsız bir kaynak ve
   * `diagramKeys` invalidation'ı paleti uçurmamalı (tersi de geçerli).
   */
  palette: () => [...componentTemplateKeys.all, 'palette'] as const
};

export const cabinetKeys = {
  all: ['cabinet'] as const,
  list: () => [...cabinetKeys.all, 'list'] as const,
  detail: (id: string) => [...cabinetKeys.all, 'detail', id] as const,
  /** Düzenleme formunun kaynağı (`GET /{id}/update`) — SCADA alanları yalnızca burada. */
  updateModel: (id: string) => [...cabinetKeys.all, 'update-model', id] as const
};

export const companyKeys = {
  all: ['company'] as const,
  list: () => [...companyKeys.all, 'list'] as const
};

export const cameraKeys = {
  all: ['camera'] as const,
  /** Kabin başına liste; `includePassive` ayrı bir anahtar — iki liste farklı veri. */
  byCabinet: (cabinetId: string, includePassive: boolean) =>
    [...cameraKeys.all, 'cabinet', cabinetId, includePassive] as const,
  detail: (id: string) => [...cameraKeys.all, 'detail', id] as const,
  /**
   * Bir kameranın çekim geçmişi.
   *
   * Bilet için anahtar YOK: bilet almak bir mutation'dır (sunucuda yol kurar,
   * önbelleğe yazar) ve önbelleklenmesi 60 saniyelik bir sırrı yeniden
   * kullanmaya çalışmak olurdu.
   */
  captures: (cameraId: string) => [...cameraKeys.all, 'captures', cameraId] as const
};

export const channelEventKeys = {
  all: ['channel-event'] as const,
  /**
   * Filtre nesnesinin TAMAMI anahtara giriyor: tarih aralığı veya kanal
   * değiştiğinde bu başka bir sorgudur. Yalnızca `cabinetId` ile anahtarlamak,
   * filtre değişiminde eski sayfayı göstermeye devam etmek olurdu.
   */
  list: (request: ChannelEventQueryRequest) => [...channelEventKeys.all, 'list', request] as const
};
