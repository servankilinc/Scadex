/**
 * Ayna: Scadex.Signalization/Dtos/Session/OperatorSessionDtos.cs
 *
 * Oturum tabloları SALT OKUNURDUR: yalnızca motor yazar, HTTP üzerinden yazım yolu yoktur (uyarı onayı
 * da yok — bayrak kalıcıdır). `...Utc` damgalarında `Z` soneki YOKTUR; `toUtcDate` / `formatUtcDateTime`.
 */
import type { CaptureStatus } from '@/models/enums/entityEnums';
import type { OperatorSessionStatus, SessionPhase } from './enums';

/** Tarih aralığı `startedAtUtc`'ye göre uygulanır. */
export interface OperatorSessionQueryRequest {
  cabinetId?: string | null;
  outerDoorId?: string | null;
  userId?: string | null;
  /** Operatörün o günkü kurum ENSTANTANESİNE (ada) göre, kurumun BUGÜNKÜ adıyla aranır. */
  authorityId?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  status?: OperatorSessionStatus | null;
  /** Verilen bayraklardan EN AZ BİRİNİ taşıyan oturumlar. */
  flags?: number | null;
  page?: number;
  /** 1-200. */
  pageSize?: number;
}

/** Özet TAMAMLANMIŞ oturumları ölçer; aralık en fazla 366 gün. */
export interface OperatorSessionSummaryRequest {
  fromUtc: string;
  toUtc: string;
  cabinetId?: string | null;
}

export interface OperatorSessionOperatorDto {
  userId: string;
  /** Oturum anındaki ad (enstantane). */
  fullName: string;
  /** Oturum anındaki kurum adı (enstantane). */
  authorityName: string;
  cardIdRaw: string;
  firstCardAtUtc: string;
  lastCardAtUtc: string;
}

export interface OperatorSessionListItemDto {
  /** IDENTITY — `number`, Guid değil. */
  id: number;
  cabinetId: string;
  cabinetName: string | null;
  outerDoorId: string;
  outerDoorName: string;
  status: OperatorSessionStatus;
  /** `SessionFlags` bit maskesi. */
  flags: number;
  startedAtUtc: string;
  endedAtUtc: string | null;
  durationSec: number | null;
  captureCount: number;
  /** Güvenlik uyarısı bayrağı var mı (`UnauthorizedEntry` / `ForcedOpen`). Kalıcıdır, onay yoktur. */
  hasAlert: boolean;
  operators: OperatorSessionOperatorDto[];
}

/** Canlı panelin satırı: yalnızca AÇIK (dış kapısı kapanmamış) oturumlar. */
export interface OperatorSessionOpenDto extends OperatorSessionListItemDto {
  phase: SessionPhase;
  /** Sunucunun yanıt anındaki geçen süre — istemci saati kaymış olabileceği için bu esas alınır. */
  elapsedSec: number;
  sirenRequested: boolean;
  cabinetSirenIsOn: boolean;
}

export interface OperatorSessionDetailDto extends OperatorSessionListItemDto {
  sirenRequestedAtUtc: string | null;
  sirenReleasedAtUtc: string | null;
  events: OperatorSessionEventDto[];
  doors: OperatorSessionDoorDto[];
  captures: OperatorSessionCaptureDto[];
}

export interface OperatorSessionEventDto {
  id: number;
  /** `SessionEventType`; geçmiş satırlarda adı olmayan bir numara (16) gelebilir. */
  type: number;
  occurredAtUtc: string;
  receivedAtUtc: string;
  innerDoorId: string | null;
  innerDoorName: string | null;
  userId: string | null;
  userFullName: string | null;
  cardIdRaw: string | null;
  deviceCommandId: string | null;
  cameraCaptureId: number | null;
  /** Anahtar (`UnknownCard`) ya da `Anahtar: mesaj` — `formatEventDetail` çevirir. */
  detail: string | null;
}

/** İç kapının oturumdaki özeti — olaylardan türetilir. */
export interface OperatorSessionDoorDto {
  innerDoorId: string;
  name: string;
  authorityName: string | null;
  firstUnlockedAtUtc: string | null;
  firstOpenedAtUtc: string | null;
  lastClosedAtUtc: string | null;
  lastLockedAtUtc: string | null;
  openCount: number;
  wasForcedOpen: boolean;
}

export interface OperatorSessionCaptureDto {
  cameraCaptureId: number;
  sequence: number;
  /** Çekim satırı çekirdekte bulunamazsa `null`. */
  status: CaptureStatus | null;
  capturedAtUtc: string | null;
  /** `wwwroot` altındaki yol; saklama süresi dolan çekimde `null` (satır kalır, dosya gider). */
  relativePath: string | null;
  failureReason: string | null;
}

export interface OperatorSessionSummaryDto {
  sessionCount: number;
  totalDurationSec: number;
  warningCount: number;
  /** Operatör başına: oturum süresi oturumdaki HER operatöre sayılır (aynı anda iki operatör çalışabilir). */
  byOperator: OperatorSessionSummaryRowDto[];
  byAuthority: OperatorSessionSummaryRowDto[];
  byCabinet: OperatorSessionSummaryRowDto[];
}

export interface OperatorSessionSummaryRowDto {
  key: string;
  name: string;
  sessionCount: number;
  totalDurationSec: number;
  averageDurationSec: number;
  /** Herhangi bir bayrağı olan oturum sayısı (yalnızca güvenlik uyarıları değil). */
  warningCount: number;
}
