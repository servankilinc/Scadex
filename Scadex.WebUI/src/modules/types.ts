import type { ComponentType } from 'react';
import type { UseQueryOptions } from '@tanstack/react-query';
import type { LucideIcon } from 'lucide-react';
import type { RouteObject } from 'react-router';
import type { PermissionCode } from '@/models/enums/entityEnums';

/** Kenar çubuğu maddesi — çekirdek menüsüyle aynı biçim. */
export interface ModuleNavItem {
  title: string;
  url: string;
  icon: LucideIcon;
  /** Verilirse madde yalnızca bu izin koduna (`Permission.Code`) sahip kullanıcıya gösterilir. Rotayı ayrıca korumak modülün işidir. */
  permission?: PermissionCode;
}

/** Ana sayfa haritasındaki otomasyon detay paneline modülün eklediği bölümün aldığı tek bilgi. */
export interface AutomationPanelSectionProps {
  automationId: string;
}

/**
 * Müşteri modülünün arayüz manifestosu. Backend'deki `Modules:<Ad>:Enabled` + `.Add<Ad>Module(...)`
 * kalıbının aynası: çekirdek modülü tanımaz, yalnızca bu sözleşmeyi okur.
 */
export interface AppModule {
  /** `VITE_MODULES` içindeki ad (küçük harf, örn. `signalization`). */
  key: string;
  /** Kenar çubuğundaki grup başlığı. */
  title: string;
  /** AppLayout'un altına eklenen rotalar. Ekranlar `lazy` yüklenmeli — modül ana pakete girmesin. */
  routes: RouteObject[];
  navItems: ModuleNavItem[];
  /**
   * Layout'a BİR KEZ takılan, ekranı olmayan bileşen (örn. canlı uyarı yoklayıcısı). `React.lazy` ile
   * verilmeli; layout onu `Suspense` içinde render eder.
   */
  LayoutExtension?: ComponentType;
  /**
   * Ana sayfa haritasında "işlem yapılıyor" ikonuyla gösterilecek otomasyonların sorgusu. Çekirdek yalnızca otomasyon
   * kimliklerini okur, "işlem"in ne demek olduğunu modül belirler. Birden çok modül verirse kimlikler birleşir.
   */
  busyAutomationsQuery?: AutomationIdsQueryOptions;
  /**
   * Ana sayfa haritasında "alarm" ikonuyla gösterilecek otomasyonların sorgusu (örn. zorla açma). Otomasyonun DURUMUNU
   * (`deviceStatusId`) değiştirmez — o ağ/donanım sağlığıdır; alarm modülün kendi kavramıdır ve ayrı çizilir. İkon
   * önceliği alarm > işlem > boşta. Birden çok modül verirse kimlikler birleşir.
   */
  alertAutomationsQuery?: AutomationIdsQueryOptions;
  /**
   * Ana sayfa haritasındaki otomasyon detay panelinin altına eklenen bölüm (örn. o otomasyonda devam eden
   * operatör işlemi). `React.lazy` ile verilmeli; çekirdek onu `Suspense` içinde render eder.
   * Bölüm kendini `@/components/custom/panel-section > PanelSection` ile çizer (açılır-kapanır kart, başlıkta özet) —
   * panel tek tip görünsün; çekirdek içeriği bilmez.
   */
  AutomationPanelSection?: ComponentType<AutomationPanelSectionProps>;
  /**
   * Ana sayfa haritasındaki otomasyon detay panelinin EN ÜSTÜNE (künyeden — Firma/Durum/… — önce)
   * eklenen eylem. `AutomationPanelSection`'dan farklı olarak devam eden bir işleme/duruma bağlı
   * OLMAYAN, panelin en sık kullanılan kısayolu için (örn. sanal kabin ekranına git). `React.lazy`
   * ile verilmeli; çekirdek onu `Suspense` içinde render eder.
   */
  AutomationPanelTopAction?: ComponentType<AutomationPanelSectionProps>;
}

/** Sonucu otomasyon kimliği listesi olan sorgu. Ham veri modülün kendi DTO'sudur; `select` onu kimliklere indirger. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- ham veri tipi modüle aittir, çekirdek yalnızca `select` sonucunu okur
export type AutomationIdsQueryOptions = UseQueryOptions<any, Error, string[], readonly unknown[]>;
