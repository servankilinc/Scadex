/**
 * Ayna: Scadex.Signalization/Model/Dtos/Config/{Queries/SignalCabinetLiveDto.cs,
 * Queries/SignalCabinetCommandResultDto.cs, Commands/SignalCabinetCommandRequest.cs} +
 * Enums/SignalEnums.cs > SignalCabinetOutput
 *
 * Sanal kabin ekranının sözleşmesi. Yapılandırma ağacıyla (`models/cabinet.ts`) aynı iskelet ama alanları DURUM
 * alanlarıdır: burası sahayı izler, kanal seçmez. Bütün durumların tek kaynağı çekirdekteki kanalın son değeridir; çıkışlarda
 * bu bir ölçüm değil, son BAŞARILI komuttur. Durum alanlarında `null` "bilinmiyor"dur.
 */
import type { CommandStatus } from '@/models/enums';

// ─────────────────────────────────────────────────────────── okuma

export interface SignalCabinetLiveDto {
  cabinetId: string;
  cabinetName: string | null;
  /** Kabin hiç yapılandırılmadıysa `false` ve ağaç boştur. */
  isConfigured: boolean;
  /** `false` ise motor bu kabinin olaylarını yok sayar — ekran bunu uyarı olarak gösterir. */
  isEnabled: boolean;
  sirenIoChannelId: string | null;
  /** **`null` = bilinmiyor** (siren tanımsız ya da hiç başarılı komut görmemiş) — kapalıdan farklıdır. */
  sirenIsOn: boolean | null;
  sirenChangedAtUtc: string | null;
  outerDoors: SignalOuterDoorLiveDto[];
}

export interface SignalOuterDoorLiveDto {
  id: string;
  name: string;
  cameraId: string | null;
  cameraName: string | null;
  switchIoChannelId: string;
  /** Anahtarın "kapı açık" anlamına gelen değeri (bilgi amaçlı; yorum sunucuda yapılır). */
  switchOpenValue: string;
  /**
   * Kapı açık mı. **`null` = bilinmiyor** (kanal pasif ya da hiç değer okunmamış) — kapalıdan farklıdır. Canlı değişim
   * `SignalDoorSwitchChanged` yayınıyla gelir.
   */
  isOpen: boolean | null;
  switchChangedAtUtc: string | null;
  /** `null` ise bu kapıya aydınlatma tanımlı değil; LED komutu sunulmaz. */
  lightIoChannelId: string | null;
  /** `null` = bilinmiyor. */
  lightIsOn: boolean | null;
  lightChangedAtUtc: string | null;
  innerDoors: SignalInnerDoorLiveDto[];
}

export interface SignalInnerDoorLiveDto {
  id: string;
  name: string;
  authorityId: string;
  authorityName: string | null;
  switchIoChannelId: string;
  /** Bkz. `SignalOuterDoorLiveDto.switchOpenValue`. */
  switchOpenValue: string;
  /** Bkz. `SignalOuterDoorLiveDto.isOpen`. */
  isOpen: boolean | null;
  switchChangedAtUtc: string | null;
  lockIoChannelId: string;
  /**
   * Kilit açık mı — ölçüm değil, kilit kanalının son başarılı komutu; `null` = bilinmiyor. Kapı açık + kilit kilitli
   * (`false`) zorlanmış açılıştır.
   */
  isUnlocked: boolean | null;
  lockChangedAtUtc: string | null;
}

// ─────────────────────────────────────────────────────────── komut

/** Kabinin yönetilen çıkışları. Hem komutun hedefi hem canlı yayının "ne değişti" alanı. */
export const SignalCabinetOutput = {
  /** Kabinin ortak sireni; hedef kimliği yoktur. */
  Siren: 1,
  /** Dış kapının aydınlatma LED'i; hedef dış kapıdır. */
  OuterDoorLight: 2,
  /** İç kapının kilit rölesi; hedef iç kapıdır. */
  InnerDoorLock: 3
} as const;
export type SignalCabinetOutput = (typeof SignalCabinetOutput)[keyof typeof SignalCabinetOutput];

export const SignalCabinetOutputLabels: Record<SignalCabinetOutput, string> = {
  [SignalCabinetOutput.Siren]: 'Siren',
  [SignalCabinetOutput.OuterDoorLight]: 'Aydınlatma',
  [SignalCabinetOutput.InnerDoorLock]: 'İç kapı kilidi'
};

export interface SignalCabinetCommandRequest {
  target: SignalCabinetOutput;
  /** Siren için `null`; aydınlatmada dış kapı, kilitte iç kapı kimliği. */
  targetId: string | null;
  /** İSTENEN NİYET, ham değer değil: siren çal / LED yak / kilit **açık**. */
  turnOn: boolean;
}

/**
 * **HTTP 200 başarı demek değildir** — sahanın cevabı `status` alanındadır (çekirdeğin komut ucuyla aynı kural).
 */
export interface SignalCabinetCommandResultDto {
  target: SignalCabinetOutput;
  targetId: string | null;
  commandId: string | null;
  status: CommandStatus;
  resultMessage: string | null;
  /** Komut sahada başarılı oldu mu (kanalın değeri çekirdekte güncellendi). */
  applied: boolean;
  /** Komuttan SONRAKİ durum (kanaldan okunur); başarısızsa değişmemiş haldir, `null` = bilinmiyor. */
  isOn: boolean | null;
  changedAtUtc: string | null;
}
