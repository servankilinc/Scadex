import { Link } from 'react-router';
import { ChevronRightIcon, MonitorIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { CabinetPanelSectionProps } from '../../types';
import { isModuleOff, useOpenSessions } from '../hooks/use-operator-sessions';

/**
 * Ana sayfa haritasındaki kabin detay panelinin EN ÜSTÜNE eklenen sinyalizasyon eylemi
 * (modülün `CabinetPanelTopAction`'ı).
 *
 * `CabinetSessionSection`'dan (panelin ALTINA eklenen bölüm) bilinçli olarak AYRI: sanal kabin
 * ekranı devam eden bir operatör işlemine bağlı değil, kabinde hiç işlem olmasa da açılabilmeli —
 * künyeden (Firma/Durum/…) önce gelmesi, panelin en sık kullanılan eylemi olduğunu da gösteriyor.
 *
 * `useOpenSessions` çağrısı `busyCabinetsQuery` ile AYNI anahtarı kullanır — harita zaten bu sorguyu
 * çalıştırdığı için ek istek doğmaz, yalnızca önbellekten `moduleOff` durumu okunur.
 */
export default function CabinetVirtualLink({ cabinetId }: CabinetPanelSectionProps) {
  const { error } = useOpenSessions();

  // Backend'de modül kapalı: eylem hiç görünmemeli (`VITE_MODULES` ayrı ayarlanır).
  if (isModuleOff(error)) return null;

  return (
    <Button
      size='lg'
      variant='outline'
      className='h-12 w-full justify-start gap-3 border-blue-500/30 bg-blue-500/10 text-blue-700 hover:bg-blue-500/15 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-400'
      nativeButton={false}
      render={<Link to={`/signalization/virtual-cabinet/${cabinetId}`} />}
    >
      <span className='flex size-8 items-center justify-center rounded-md bg-blue-500/15'>
        <MonitorIcon className='size-4' />
      </span>
      <span className='flex-1 text-left text-sm font-semibold'>Sanal Kabin</span>
      <ChevronRightIcon className='size-4 opacity-60' />
    </Button>
  );
}
