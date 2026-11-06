/**
 * TanStack Query anahtarları.
 */
import type { ChannelEventQueryRequest } from '@/models/channelEvent';

export const diagramKeys = {
  all: ['diagram'] as const,
  /** Bir otomasyonun tüm grafı — canvas ayarları DAHİL (aggregate'in içinde gelir). */
  automation: (automationId: string) => [...diagramKeys.all, 'automation', automationId] as const,
  /** Bir cihazın komut geçmişi. */
  deviceCommands: (deviceId: string) => [...diagramKeys.all, 'device', deviceId, 'commands'] as const
};

export const componentTemplateKeys = {
  all: ['component-template'] as const,
  /**
   * Palet her otomasyonda aynı olduğu için uzun `staleTime` ile cachelenir. Anahtar
   * diyagramın ALTINDA DEĞİL: şablon kütüphanesi otomasyondan bağımsız bir kaynak ve
   * `diagramKeys` invalidation'ı paleti uçurmamalı (tersi de geçerli).
   */
  palette: () => [...componentTemplateKeys.all, 'palette'] as const,
  /** Yönetim ekranının tip kartları (tip başına sayı). */
  typeCounts: () => [...componentTemplateKeys.all, 'type-counts'] as const,
  /** Yönetim ekranının tablosu — tek tipe süzülü, pinsiz liste. */
  byType: (deviceTypeId: number) => [...componentTemplateKeys.all, 'by-type', deviceTypeId] as const
};

export const automationKeys = {
  all: ['automation'] as const,
  list: () => [...automationKeys.all, 'list'] as const,
  detail: (id: string) => [...automationKeys.all, 'detail', id] as const,
  /** Düzenleme formunun kaynağı (`GET /{id}/update`) — SCADA alanları yalnızca burada. */
  updateModel: (id: string) => [...automationKeys.all, 'update-model', id] as const,
  /** Otomasyon durumunun dökümü (harita paneli). Canlı yayın + yedek yoklama tazeler. */
  statusBreakdown: (id: string) => [...automationKeys.all, 'status-breakdown', id] as const
};

export const companyKeys = {
  all: ['company'] as const,
  list: () => [...companyKeys.all, 'list'] as const
};

// Kamera anahtarları (cameraKeys, çekim ayarı) CameraManagement modülündedir: modules/cameramanagement/api/query-keys.ts

export const channelEventKeys = {
  all: ['channel-event'] as const,
  /**
   * Filtre nesnesinin TAMAMI anahtara giriyor: tarih aralığı veya kanal
   * değiştiğinde bu başka bir sorgudur. Yalnızca `automationId` ile anahtarlamak,
   * filtre değişiminde eski sayfayı göstermeye devam etmek olurdu.
   */
  list: (request: ChannelEventQueryRequest) => [...channelEventKeys.all, 'list', request] as const
};

export const userKeys = {
  all: ['user'] as const,
  /** Pasifler DAHİL — geri alınabilsinler diye. */
  list: () => [...userKeys.all, 'list'] as const,
  /** Kullanıcının rol ADLARI (Identity adla döner) — Roller dialogunun kaynağı. */
  roles: (userId: string) => [...userKeys.all, 'roles', userId] as const,
  /** Kart tanımlama penceresinin dar listesi (aktifler) — `ManageAccessCards`. `all` altında: kart atanınca birlikte tazelenir. */
  cardHolders: () => [...userKeys.all, 'card-holders'] as const,
  /** Giriş yapan kullanıcının kendi profili (`/profile`). `all` altında: yönetici kendini düzenleyince birlikte tazelenir. */
  profile: () => [...userKeys.all, 'profile'] as const
};

export const roleKeys = {
  all: ['role'] as const,
  list: () => [...roleKeys.all, 'list'] as const,
  /** Bir rolün izinleri — İzinler dialogunun kaynağı. */
  permissions: (roleId: string) => [...roleKeys.all, 'permissions', roleId] as const,
  /** Tüm roller × izin kimlikleri — kullanıcı Roller dialogundaki önizleme. */
  matrix: () => [...roleKeys.all, 'matrix'] as const,
  /** Role atanmış kullanıcılar — Üyeler dialogu. */
  members: (roleId: string) => [...roleKeys.all, 'members', roleId] as const
};

/**
 * İzin kataloğu seed'den gelir (`IImmutableEntity`) ve çalışma anında değişmez;
 * `staleTime: Infinity` ile bir kez çekilir. `roleKeys`'in ALTINDA değil: rol
 * invalidation'ı kataloğu yeniden çektirmemeli.
 */
export const permissionKeys = {
  list: () => ['permission', 'list'] as const
};
