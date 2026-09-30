import { redirect, type RouteObject } from 'react-router';
import { MonitorIcon } from 'lucide-react';
import { hasPermission } from '@/lib/auth-session';
import type { Permission } from '@/models/enums/entityEnums';
import type { AppModule } from '../types';

/**
 * RemoteDesk modülü — PC ekranlarını canlı izleme (backend: `Scadex.RemoteDesk`, şartname: depo kökündeki RemoteDesk.md).
 *
 * Bu dosya ana pakete girer (menü + rota tanımı); ekranlar `lazy`. Modül kapalı kurulumda (`VITE_MODULES`'ta `remotedesk` yok)
 * kullanıcı bu kodun hiçbirini indirmez. Rotalar `/remote-desk` altında.
 */
/** Sunucudaki policy adıyla aynı izin kodu; modülün tüm uçları bunu ister (yoksa 403). */
const VIEW_PERMISSION: keyof typeof Permission = 'RemotePcView';

const routes: RouteObject[] = [
  {
    path: 'remote-desk',
    handle: { crumb: 'PC Ekranları' },
    // İzni olmayan adresi elle yazsa da ana sayfaya döner (menü maddesi zaten gizli). Asıl denetim sunucudadır.
    loader: () => (hasPermission(VIEW_PERMISSION) ? null : redirect('/')),
    children: [
      { index: true, loader: () => redirect('/remote-desk/pcs') },
      {
        path: 'pcs',
        children: [
          { index: true, lazy: async () => ({ Component: (await import('./views/pcs')).default }) },
          {
            path: ':deviceId',
            handle: { crumb: 'PC' },
            lazy: async () => ({ Component: (await import('./views/pc-detail')).default })
          }
        ]
      }
    ]
  }
];

export const remoteDeskModule: AppModule = {
  key: 'remotedesk',
  title: 'Uzak Masaüstü',
  routes,
  navItems: [{ title: 'PC Ekranları', url: '/remote-desk/pcs', icon: MonitorIcon, permission: VIEW_PERMISSION }]
};
