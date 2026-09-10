/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramIoChannelDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { PinDirection } from '@/models/enums';

/**
 * Cihazın telemetri kanalının STATİK tanımı.
 *
 * `currentValue` / `valueUpdatedAt` BİLEREK yok. Canlı değer SignalR'dan akar ve
 * istemcide TanStack Query cache'ine DEĞİL, ayrı bir harici store'a yazılır —
 * aksi halde editörün değişiklik günlüğü telemetriyi "kullanıcı düzenlemesi"
 * sanıp SCADA değerlerini sunucuya geri yazmaya çalışır.
 */
export interface DiagramIoChannelDto {
  id: string;
  channelNumber: number;
  direction: PinDirection;
  isEnabled: boolean;
  name: string;
}
