import { lazy } from 'react';
import { redirect, type RouteObject } from 'react-router';
import { ChartColumnIcon, DoorOpenIcon, IdCardIcon, LandmarkIcon, SlidersHorizontalIcon } from 'lucide-react';
import type { AppModule } from '../types';
import { busyCabinetsQueryOptions } from './hooks/use-operator-sessions';

/**
 * Sinyalizasyon modülü — operatör işlemi takibi (backend: `Scadex.Signalization`, PROJECT_OVERVIEW.md § 10).
 *
 * Bu dosya ana pakete girer (menü ve rota tanımı), bu yüzden ekran İÇERMEZ: bütün ekranlar ve uyarı
 * yoklayıcısı `lazy`. Ana pakete giren tek veri kodu haritanın sorgu tanımıdır (api çağrısı + anahtar). Modül kapalı bir kurulumda (`VITE_MODULES` boş) kullanıcı bu kodun hiçbirini indirmez.
 *
 * Rotaların hepsi `/signalization` altında: modül menüsü kendi grubunda durur ve çekirdeğin `/admin` ağacına
 * karışmaz.
 */
const routes: RouteObject[] = [
  {
    path: 'signalization',
    handle: { crumb: 'Sinyalizasyon' },
    children: [
      // Breadcrumb'daki "Sinyalizasyon" bağlantısı boş bir sayfaya düşmesin.
      { index: true, loader: () => redirect('/signalization/sessions') },
      {
        path: 'sessions',
        handle: { crumb: 'Operatör İşlemleri' },
        children: [
          { index: true, lazy: async () => ({ Component: (await import('./views/sessions')).default }) },
          {
            path: ':sessionId',
            handle: { crumb: 'İşlem Detayı' },
            lazy: async () => ({ Component: (await import('./views/sessions/detail')).default })
          }
        ]
      },
      {
        path: 'report',
        handle: { crumb: 'Rapor' },
        lazy: async () => ({ Component: (await import('./views/report')).default })
      },
      {
        // Kenar çubuğunda YOK: ekrana yalnızca ana sayfa haritasındaki kabin panelinden girilir — hangi kabine
        // bakıldığı oradan belli, kabin seçtiren ikinci bir giriş gereksiz olurdu.
        path: 'virtual-cabinet',
        handle: { crumb: 'Sanal Kabin' },
        children: [
          {
            path: ':cabinetId',
            children: [
              { index: true, lazy: async () => ({ Component: (await import('./views/virtual-cabinet')).default }) },
              {
                path: ':outerDoorId',
                handle: { crumb: 'Dış Kapı' },
                lazy: async () => ({ Component: (await import('./views/virtual-cabinet/detail')).default })
              }
            ]
          }
        ]
      },
      {
        path: 'cabinets',
        handle: { crumb: 'Kapı Yapılandırması' },
        lazy: async () => ({ Component: (await import('./views/admin/cabinet-config')).default })
      },
      {
        path: 'authorities',
        handle: { crumb: 'Kurumlar' },
        lazy: async () => ({ Component: (await import('./views/admin/authorities')).default })
      },
      {
        path: 'operators',
        handle: { crumb: 'Operatörler' },
        lazy: async () => ({ Component: (await import('./views/admin/operators')).default })
      }
    ]
  }
];

export const signalizationModule: AppModule = {
  key: 'signalization',
  title: 'Sinyalizasyon',
  routes,
  navItems: [
    { title: 'Operatör İşlemleri', url: '/signalization/sessions', icon: DoorOpenIcon },
    { title: 'Rapor', url: '/signalization/report', icon: ChartColumnIcon },
    { title: 'Kapı Yapılandırması', url: '/signalization/cabinets', icon: SlidersHorizontalIcon },
    { title: 'Kurumlar', url: '/signalization/authorities', icon: LandmarkIcon },
    { title: 'Operatörler', url: '/signalization/operators', icon: IdCardIcon }
  ],
  // Oturum açık olduğu sürece her sayfada: operatör işlemi canlı yayını + kartsız giriş / zorla açma bildirimi.
  LayoutExtension: lazy(() => import('./components/layout-extension')),
  // Ana sayfa haritası: açık operatör işlemi olan kabin "işlem yapılıyor" ikonuyla çizilir. Uyarı
  // yoklayıcısıyla aynı sorgu anahtarı — ek istek doğmaz. Anlık güncellemeyi canlı yayın verir.
  busyCabinetsQuery: busyCabinetsQueryOptions(),
  // Haritadaki kabin detay paneli: o kabinde devam eden / en son biten işlem ve sahadaki operatörler.
  // `lazy` — kullanıcı bir kabinin detayını açana dek indirilmez.
  CabinetPanelSection: lazy(() => import('./components/cabinet-session-section'))
};
