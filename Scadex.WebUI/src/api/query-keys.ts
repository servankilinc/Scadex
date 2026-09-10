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

/**
 * Sistem ayarları.
 *
 * Ayar grupları AYRI anahtarlar altında: sunucuda da ayrı tablo, ayrı servis ve ayrı
 * önbellek anahtarı var. Ortak bir `all` ile invalidate etmek, medya geçidini
 * kaydeden kullanıcıya kamera ayarlarını da yeniden çektirirdi.
 */
export const settingKeys = {
  mediaGateway: () => ['setting', 'media-gateway'] as const,
  cameraCapture: () => ['setting', 'camera-capture'] as const
};

export const userKeys = {
  all: ['user'] as const,
  /** Pasifler DAHİL — geri alınabilsinler diye. */
  list: () => [...userKeys.all, 'list'] as const,
  /** Kullanıcının rol ADLARI (Identity adla döner) — Roller dialogunun kaynağı. */
  roles: (userId: string) => [...userKeys.all, 'roles', userId] as const
};

export const roleKeys = {
  all: ['role'] as const,
  list: () => [...roleKeys.all, 'list'] as const,
  /** Bir rolün izinleri — İzinler dialogunun kaynağı. */
  permissions: (roleId: string) => [...roleKeys.all, 'permissions', roleId] as const
};

/**
 * İzin kataloğu seed'den gelir (`IImmutableEntity`) ve çalışma anında değişmez;
 * `staleTime: Infinity` ile bir kez çekilir. `roleKeys`'in ALTINDA değil: rol
 * invalidation'ı kataloğu yeniden çektirmemeli.
 */
export const permissionKeys = {
  list: () => ['permission', 'list'] as const
};
