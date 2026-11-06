import type { RouteObject } from 'react-router';
import { SettingsIcon, VideoIcon } from 'lucide-react';
import { requirePermission, type PermissionCode } from '@/lib/route-guards';
import type { AppModule } from '../types';

/**
 * CameraManagement modülü — kamera canlı izleme, anlık görüntü, kare/klip çekimi ve çekim ayarları
 * (backend: `Modules/CameraManagement/Scadex.CameraManagement`).
 *
 * Kameranın CİHAZ kısmı çekirdektedir: diyagramdaki kamera alanları / oluşturma penceresi, `CameraBrand`, `StreamProfile`
 * ve WHEP istemcisi (`@/lib/media/whep`, RemoteDesk de kullanır). Sinyalizasyon çözümü bu modüle bağımlıdır
 * (izleme penceresi, kare adresi) — `VITE_MODULES`'ta `signalization` varsa `cameramanagement` de olmalı.
 *
 * Bu dosya ana pakete girer (menü + rota tanımı); ekranlar `lazy` — WebRTC/WHEP kodu, hiç kamera izlemeyen kullanıcının ana
 * paketine girmez. Rotalar `/cameras` altında.
 */
const VIEW_PERMISSION: PermissionCode = 'ViewCamera';
const SETTINGS_PERMISSION: PermissionCode = 'ConfigureSystem';

// `requirePermission` üst rotada DEĞİL, her çocukta ayrı: çekim ayarı `ConfigureSystem` ister, kamera izni istemez.
const routes: RouteObject[] = [
  {
    path: 'cameras',
    handle: { crumb: 'Kameralar' },
    children: [
      {
        index: true,
        loader: requirePermission(VIEW_PERMISSION),
        lazy: async () => ({ Component: (await import('./views/cameras')).default })
      },
      {
        // Eski `/admin/settings`: tek içeriği kamera çekim ayarıydı.
        path: 'settings',
        handle: { crumb: 'Çekim Ayarları' },
        loader: requirePermission(SETTINGS_PERMISSION),
        lazy: async () => ({ Component: (await import('./views/settings')).default })
      },
      {
        // Otomasyonun kameraları: izleme + bilgi güncelleme. Statik `automation` bölümü `:cameraId`'den önce eşleşir.
        path: 'automation/:automationId',
        handle: { crumb: 'Otomasyon' },
        loader: requirePermission(VIEW_PERMISSION),
        lazy: async () => ({ Component: (await import('./views/cameras/automation')).default })
      },
      {
        path: ':cameraId',
        handle: { crumb: 'Kamera' },
        loader: requirePermission(VIEW_PERMISSION),
        lazy: async () => ({ Component: (await import('./views/cameras/detail')).default })
      }
    ]
  }
];

export const cameraManagementModule: AppModule = {
  key: 'cameramanagement',
  title: 'Kamera',
  routes,
  navItems: [
    { title: 'Kameralar', url: '/cameras', icon: VideoIcon, permission: VIEW_PERMISSION },
    { title: 'Çekim Ayarları', url: '/cameras/settings', icon: SettingsIcon, permission: SETTINGS_PERMISSION }
  ]
};
