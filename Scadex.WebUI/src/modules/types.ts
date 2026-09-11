import type { ComponentType } from 'react';
import type { LucideIcon } from 'lucide-react';
import type { RouteObject } from 'react-router';

/** Kenar çubuğu maddesi — çekirdek menüsüyle aynı biçim. */
export interface ModuleNavItem {
  title: string;
  url: string;
  icon: LucideIcon;
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
}
