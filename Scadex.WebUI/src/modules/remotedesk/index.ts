import { redirect, type RouteObject } from 'react-router';
import { ClipboardListIcon, MonitorIcon } from 'lucide-react';
import { hasPermission } from '@/lib/auth-session';
import type { Permission } from '@/models/enums/entityEnums';
import type { AppModule } from '../types';

/**
 * RemoteDesk modülü — PC ekranlarını canlı izleme (backend: `Scadex.RemoteDesk`, şartname: depo kökündeki RemoteDesk.md).
 *
 * Bu dosya ana pakete girer (menü + rota tanımı); ekranlar `lazy`. Modül kapalı kurulumda (`VITE_MODULES`'ta `remotedesk` yok)
 * kullanıcı bu kodun hiçbirini indirmez. Rotalar `/remote-desk` altında.
 */
/** Sunucudaki policy adlarıyla aynı izin kodları: izleme uçları VIEW, rapor uçları AUDIT ister (yoksa 403). */
const VIEW_PERMISSION: keyof typeof Permission = 'RemotePcView';
const AUDIT_PERMISSION: keyof typeof Permission = 'RemotePcAudit';

/**
 * İzni olmayan adresi elle yazsa da ana sayfaya döner (menü maddesi zaten gizli). Asıl denetim sunucudadır. Üst rotada DEĞİL,
 * her çocukta ayrı: izleme ve rapor ayrı izinlerdir, yalnızca rapor izni olan kullanıcı PC listesine düşmemeli.
 */
const requirePermission = (permission: string) => () => (hasPermission(permission) ? null : redirect('/'));

const routes: RouteObject[] = [
  {
    path: 'remote-desk',
    handle: { crumb: 'PC Ekranları' },
    children: [
      { index: true, loader: () => redirect(hasPermission(VIEW_PERMISSION) ? '/remote-desk/pcs' : '/remote-desk/reports') },
      {
        path: 'reports',
        handle: { crumb: 'İzleme Raporu' },
        loader: requirePermission(AUDIT_PERMISSION),
        lazy: async () => ({ Component: (await import('./views/reports')).default })
      },
      {
        path: 'pcs',
        loader: requirePermission(VIEW_PERMISSION),
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
  navItems: [
    { title: 'PC Ekranları', url: '/remote-desk/pcs', icon: MonitorIcon, permission: VIEW_PERMISSION },
    { title: 'İzleme Raporu', url: '/remote-desk/reports', icon: ClipboardListIcon, permission: AUDIT_PERMISSION }
  ]
};
