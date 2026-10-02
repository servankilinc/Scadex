import {
  CpuIcon,
  GaugeIcon,
  IdCardIcon,
  LightbulbIcon,
  LogInIcon,
  LogOutIcon,
  MonitorIcon,
  PlugZapIcon,
  PuzzleIcon,
  RadarIcon,
  Rows3Icon,
  ToggleLeftIcon,
  ZapIcon,
  type LucideIcon
} from 'lucide-react';
import { DeviceType } from '@/models/enums';

/**
 * DeviceType başına TEK ikon — diyagram paletinin ağacı (grup başlığı + şablon kartı) ve
 * `/admin/templates` tip kartları aynı haritadan okur; bir tipin ikonu iki ekranda ayrışmasın.
 */
export const DEVICE_TYPE_ICON: Record<DeviceType, LucideIcon> = {
  [DeviceType.ControlModule]: CpuIcon,
  [DeviceType.InputModule]: LogInIcon,
  [DeviceType.OutputModule]: LogOutIcon,
  [DeviceType.LedModule]: LightbulbIcon,
  [DeviceType.TerminalBlock]: Rows3Icon,
  [DeviceType.Sensor]: RadarIcon,
  [DeviceType.Peripheral]: PuzzleIcon,
  [DeviceType.PowerSupply]: ZapIcon,
  [DeviceType.MeasurementDevice]: GaugeIcon,
  [DeviceType.CardReader]: IdCardIcon,
  [DeviceType.Mains]: PlugZapIcon,
  [DeviceType.CircuitBreaker]: ToggleLeftIcon,
  [DeviceType.Pc]: MonitorIcon
};

/**
 * Tiplerin gösterim sırası: `DeviceType`'ın TANIM sırası (ControlModule → Pc). API yanıtının
 * sırasına bağlı kalınmıyor — sunucu farklı bir sırayla dönerse grupların yeri değişir, kullanıcı
 * her seferinde aynı yerde arar.
 */
export const DEVICE_TYPE_ORDER = Object.values(DeviceType) as DeviceType[];
