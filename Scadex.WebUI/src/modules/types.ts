import type { ComponentType } from 'react';
import type { UseQueryOptions } from '@tanstack/react-query';
import type { LucideIcon } from 'lucide-react';
import type { RouteObject } from 'react-router';

/** Kenar çubuğu maddesi — çekirdek menüsüyle aynı biçim. */
export interface ModuleNavItem {
  title: string;
  url: string;
  icon: LucideIcon;
}

/** Ana sayfa haritasındaki kabin detay paneline modülün eklediği bölümün aldığı tek bilgi. */
export interface CabinetPanelSectionProps {
  cabinetId: string;
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
   * Ana sayfa haritasında "işlem yapılıyor" ikonuyla gösterilecek kabinlerin sorgusu. Çekirdek yalnızca kabin
   * kimliklerini okur, "işlem"in ne demek olduğunu modül belirler. Birden çok modül verirse kimlikler birleşir.
   */
  busyCabinetsQuery?: CabinetIdsQueryOptions;
  /**
   * Ana sayfa haritasında "alarm" ikonuyla gösterilecek kabinlerin sorgusu (örn. zorla açma). Kabinin DURUMUNU
   * (`deviceStatusId`) değiştirmez — o ağ/donanım sağlığıdır; alarm modülün kendi kavramıdır ve ayrı çizilir. İkon
   * önceliği alarm > işlem > boşta. Birden çok modül verirse kimlikler birleşir.
   */
  alertCabinetsQuery?: CabinetIdsQueryOptions;
  /**
   * Ana sayfa haritasındaki kabin detay panelinin altına eklenen bölüm (örn. o kabinde devam eden
   * operatör işlemi). `React.lazy` ile verilmeli; çekirdek onu `Suspense` içinde render eder.
   * Üst ayırıcısını (`border-t`) bölümün kendisi çizer — çekirdek içeriği bilmez.
   */
  CabinetPanelSection?: ComponentType<CabinetPanelSectionProps>;
  /**
   * Ana sayfa haritasındaki kabin detay panelinin EN ÜSTÜNE (künyeden — Firma/Durum/… — önce)
   * eklenen eylem. `CabinetPanelSection`'dan farklı olarak devam eden bir işleme/duruma bağlı
   * OLMAYAN, panelin en sık kullanılan kısayolu için (örn. sanal kabin ekranına git). `React.lazy`
   * ile verilmeli; çekirdek onu `Suspense` içinde render eder.
   */
  CabinetPanelTopAction?: ComponentType<CabinetPanelSectionProps>;
}

/** Sonucu kabin kimliği listesi olan sorgu. Ham veri modülün kendi DTO'sudur; `select` onu kimliklere indirger. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- ham veri tipi modüle aittir, çekirdek yalnızca `select` sonucunu okur
export type CabinetIdsQueryOptions = UseQueryOptions<any, Error, string[], readonly unknown[]>;
