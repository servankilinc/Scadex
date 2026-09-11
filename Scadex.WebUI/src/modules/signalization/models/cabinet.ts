/**
 * Ayna: Scadex.Signalization/Dtos/Config/{Queries/SignalCabinetDto.cs, Commands/SignalCabinetSaveRequest.cs}
 *
 * Kabin yapılandırması: kabin (ortak siren) → dış kapılar (anahtar, kamera) → iç kapılar (kurum, anahtar,
 * kilit). Kapılar SANALDIR — ilişki cihaza değil kanala (`IoChannel`) kurulur.
 */
import { z } from 'zod';
import type { SignalAuthorityDto } from './authority';

// ─────────────────────────────────────────────────────────── okuma

/** Kabin hiç yapılandırılmadıysa `isConfigured = false` ve varsayılanlar döner. */
export interface SignalCabinetDto {
  cabinetId: string;
  cabinetName: string | null;
  isConfigured: boolean;
  isEnabled: boolean;
  sirenIoChannelId: string | null;
  sirenDurationSec: number;
  entrySnapshotCount: number;
  entrySnapshotIntervalMs: number;
  awaitingCardTimeoutSec: number;
  sessionMaxDurationMin: number;
  /** Sirenin fiziksel durumu (salt okunur). */
  sirenIsOn: boolean;
  outerDoors: SignalOuterDoorDto[];
}

export interface SignalOuterDoorDto {
  id: string;
  name: string;
  switchIoChannelId: string;
  switchOpenValue: string;
  cameraId: string | null;
  innerDoors: SignalInnerDoorDto[];
}

export interface SignalInnerDoorDto {
  id: string;
  name: string;
  authorityId: string;
  switchIoChannelId: string;
  switchOpenValue: string;
  lockIoChannelId: string;
  unlockTurnsOn: boolean;
  /** Kilidin son bilinen durumu (salt okunur; yazım yalnızca motordan). */
  isUnlocked: boolean;
}

export interface SignalCabinetOptionsDto {
  inputChannels: SignalChannelOptionDto[];
  outputChannels: SignalChannelOptionDto[];
  cameras: SignalCameraOptionDto[];
  /** Yalnızca AKTİF kurumlar. */
  authorities: SignalAuthorityDto[];
}

export interface SignalChannelOptionDto {
  id: string;
  channelNumber: number;
  /** SCADA adresi: `IN3`, `OUT1`. */
  address: string;
  channelName: string;
  /** Kanalın kartı (Device). */
  deviceName: string | null;
  /** Kanalın pinine TEK ADIMLIK kabloyla bağlı saha cihazının adı — yalnızca etiket. */
  wiredDeviceName: string | null;
  currentValue: string | null;
  /** KAYITLI yapılandırmada bu kanalı kullanan kapı. */
  usedBy: string | null;
}

export interface SignalCameraOptionDto {
  id: string;
  name: string;
  isActive: boolean;
}

// ─────────────────────────────────────────────────────────── yazma

/**
 * TAM ağaç: delta değil. Sunucu kimliğe göre upsert eder, gövdede olmayan kapıyı PASİFE alır (fiziksel
 * silme yok — geçmiş oturum olayları kapı kimliğini gösterir).
 */
export interface SignalCabinetSaveRequest {
  isEnabled: boolean;
  sirenIoChannelId: string | null;
  sirenDurationSec: number;
  entrySnapshotCount: number;
  entrySnapshotIntervalMs: number;
  awaitingCardTimeoutSec: number;
  sessionMaxDurationMin: number;
  outerDoors: {
    id: string;
    name: string;
    switchIoChannelId: string;
    switchOpenValue: string;
    cameraId: string | null;
    innerDoors: {
      id: string;
      name: string;
      authorityId: string;
      switchIoChannelId: string;
      switchOpenValue: string;
      lockIoChannelId: string;
      unlockTurnsOn: boolean;
    }[];
  }[];
}

// ─────────────────────────────────────────────────────────── form

const intBetween = (min: number, max: number, message: string) => z.number({ error: 'Bir sayı girin' }).int(message).min(min, message).max(max, message);

/**
 * Sunucudaki `SignalCabinetSaveRequestValidator` + alt validator'ların kopyası — **sunucudaki kural
 * değişirse burası da elle değişmeli.** `superRefine`, sunucunun referans doğrulamasının gövde içinde
 * görülebilen iki kuralını erken gösterir (kanal tek kapıda, kurum kabinde tek iç kapıda); kanal yönü,
 * kameranın kabine aitliği ve kurumun aktifliği yalnızca sunucuda denetlenir. Son söz sunucudadır.
 *
 * Kimlik alanları formda `doorId` adını taşır: `useFieldArray` kendi `id` anahtarını üretir.
 * Seçimsiz alanlar (`sirenIoChannelId`, `cameraId`) formda `''`, gövdede `null`'dır.
 */
export const signalCabinetFormSchema = z
  .object({
    isEnabled: z.boolean(),
    sirenIoChannelId: z.string(),
    sirenDurationSec: intBetween(1, 3600, 'Siren süresi 1-3600 saniye arasında olmalı'),
    entrySnapshotCount: intBetween(0, 10, 'Kare sayısı 0-10 arasında olmalı'),
    entrySnapshotIntervalMs: intBetween(200, 10000, 'Kare aralığı 200-10000 ms arasında olmalı'),
    awaitingCardTimeoutSec: intBetween(0, 3600, 'Kart bekleme süresi 0-3600 saniye arasında olmalı (0 = kapalı)'),
    sessionMaxDurationMin: intBetween(1, 1440, 'Oturum süresi 1-1440 dakika arasında olmalı'),
    outerDoors: z.array(
      z.object({
        doorId: z.string(),
        name: z.string().trim().min(1, 'Dış kapı adı zorunlu').max(128, 'Dış kapı adı en fazla 128 karakter olabilir'),
        switchIoChannelId: z.string().min(1, 'Dış kapı anahtar kanalı seçilmeli'),
        switchOpenValue: z.string().trim().min(1, "Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)").max(16, "Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)"),
        cameraId: z.string(),
        innerDoors: z
          .array(
            z.object({
              doorId: z.string(),
              name: z.string().trim().min(1, 'İç kapı adı zorunlu').max(128, 'İç kapı adı en fazla 128 karakter olabilir'),
              authorityId: z.string().min(1, 'Kurum seçilmeli'),
              switchIoChannelId: z.string().min(1, 'İç kapı anahtar kanalı seçilmeli'),
              switchOpenValue: z.string().trim().min(1, "Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)").max(16, "Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)"),
              lockIoChannelId: z.string().min(1, 'Kilit kanalı seçilmeli'),
              unlockTurnsOn: z.boolean()
            })
          )
          .min(1, 'Her dış kapının en az bir iç kapısı olmalı')
      })
    )
  })
  .superRefine((values, ctx) => {
    // Sunucudaki ValidateReferencesAsync ile aynı sıra ve aynı mesajlar: önce siren, sonra kapılar.
    const channelOwner = new Map<string, string>();
    if (values.sirenIoChannelId) channelOwner.set(values.sirenIoChannelId, 'kabin sireni');

    const claim = (channelId: string, owner: string, path: (string | number)[]) => {
      if (!channelId) return;
      const existing = channelOwner.get(channelId);
      if (existing) ctx.addIssue({ code: 'custom', path, message: `Bu kanal zaten kullanılıyor: ${existing}.` });
      else channelOwner.set(channelId, owner);
    };

    const authorityOwner = new Map<string, string>();

    values.outerDoors.forEach((outer, i) => {
      claim(outer.switchIoChannelId, outer.name, ['outerDoors', i, 'switchIoChannelId']);

      outer.innerDoors.forEach((inner, j) => {
        const base = ['outerDoors', i, 'innerDoors', j];

        if (inner.authorityId) {
          const owner = authorityOwner.get(inner.authorityId);
          if (owner) ctx.addIssue({ code: 'custom', path: [...base, 'authorityId'], message: `Bu kurumun kabinde zaten bir iç kapısı var: ${owner}.` });
          else authorityOwner.set(inner.authorityId, inner.name);
        }

        claim(inner.switchIoChannelId, `${inner.name} (anahtar)`, [...base, 'switchIoChannelId']);
        claim(inner.lockIoChannelId, `${inner.name} (kilit)`, [...base, 'lockIoChannelId']);
      });
    });
  });

export type SignalCabinetFormValues = z.infer<typeof signalCabinetFormSchema>;
export type SignalOuterDoorFormValues = SignalCabinetFormValues['outerDoors'][number];
export type SignalInnerDoorFormValues = SignalOuterDoorFormValues['innerDoors'][number];

export function toCabinetFormValues(dto: SignalCabinetDto): SignalCabinetFormValues {
  return {
    isEnabled: dto.isEnabled,
    sirenIoChannelId: dto.sirenIoChannelId ?? '',
    sirenDurationSec: dto.sirenDurationSec,
    entrySnapshotCount: dto.entrySnapshotCount,
    entrySnapshotIntervalMs: dto.entrySnapshotIntervalMs,
    awaitingCardTimeoutSec: dto.awaitingCardTimeoutSec,
    sessionMaxDurationMin: dto.sessionMaxDurationMin,
    outerDoors: dto.outerDoors.map(outer => ({
      doorId: outer.id,
      name: outer.name,
      switchIoChannelId: outer.switchIoChannelId,
      switchOpenValue: outer.switchOpenValue,
      cameraId: outer.cameraId ?? '',
      innerDoors: outer.innerDoors.map(inner => ({
        doorId: inner.id,
        name: inner.name,
        authorityId: inner.authorityId,
        switchIoChannelId: inner.switchIoChannelId,
        switchOpenValue: inner.switchOpenValue,
        lockIoChannelId: inner.lockIoChannelId,
        unlockTurnsOn: inner.unlockTurnsOn
      }))
    }))
  };
}

export function toCabinetSaveRequest(values: SignalCabinetFormValues): SignalCabinetSaveRequest {
  return {
    isEnabled: values.isEnabled,
    sirenIoChannelId: values.sirenIoChannelId || null,
    sirenDurationSec: values.sirenDurationSec,
    entrySnapshotCount: values.entrySnapshotCount,
    entrySnapshotIntervalMs: values.entrySnapshotIntervalMs,
    awaitingCardTimeoutSec: values.awaitingCardTimeoutSec,
    sessionMaxDurationMin: values.sessionMaxDurationMin,
    outerDoors: values.outerDoors.map(outer => ({
      id: outer.doorId,
      name: outer.name.trim(),
      switchIoChannelId: outer.switchIoChannelId,
      switchOpenValue: outer.switchOpenValue.trim(),
      cameraId: outer.cameraId || null,
      innerDoors: outer.innerDoors.map(inner => ({
        id: inner.doorId,
        name: inner.name.trim(),
        authorityId: inner.authorityId,
        switchIoChannelId: inner.switchIoChannelId,
        switchOpenValue: inner.switchOpenValue.trim(),
        lockIoChannelId: inner.lockIoChannelId,
        unlockTurnsOn: inner.unlockTurnsOn
      }))
    }))
  };
}
