/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Commands/ComponentTemplateCreateRequest.cs — sözleşme: docs/api-contract/10-component-template.md */
import { z } from 'zod';
import { HandleSide, PinDirection, PinFunction, VoltageLevel } from '@/models/enums';

/**
 * `POST /api/ComponentTemplate` gövdesi — şablon + pinler tek transaction'da.
 *
 * **Pin taslakları `componentTemplateId` TAŞIMAZ.** Bu ucun bütün varlık sebebi
 * o: pinlerin FK'si için gerçek bir şablon Id'si gerekiyor, ama şablon henüz
 * oluşmadığı için yazılabilecek bir değer yok. Sunucu commit'ten önce kendisi
 * bağlar.
 *
 * Sınırlar backend validator'ıyla birebir aynı tutulmalıdır; burada gevşek
 * bırakılırsa kullanıcı formu geçer ama sunucudan 400 alır — üstelik bu uçta
 * 400, bütün şablonun kaybolması demek.
 */

/** Sunucudaki `ComponentTemplateCreateRequestValidator.MaxPins` ile aynı. */
export const TEMPLATE_MAX_PINS = 256;

/** Sayısal enum değerlerinden zod birleşimi üretir — `as const` enum aynalarımız için. */
function enumValue<T extends number>(values: readonly T[]) {
  return z.union(values.map(value => z.literal(value)) as unknown as [z.ZodLiteral<T>, z.ZodLiteral<T>, ...z.ZodLiteral<T>[]]);
}

const HANDLE_SIDES = [HandleSide.Left, HandleSide.Right, HandleSide.Top, HandleSide.Bottom] as const;

const PIN_FUNCTIONS = [
  PinFunction.COM,
  PinFunction.NO,
  PinFunction.NC,
  PinFunction.VCC,
  PinFunction.GND,
  PinFunction.RS485_POS,
  PinFunction.RS485_NEG,
  PinFunction.RJ45,
  PinFunction.LED_Anode,
  PinFunction.LED_Cathode,
  PinFunction.Signal_In,
  PinFunction.Signal_Out,
  PinFunction.Analog_In,
  PinFunction.DryContact,
  PinFunction.Line_L,
  PinFunction.Neutral_N,
  PinFunction.Earth_PE,
  PinFunction.General
] as const;

const PIN_DIRECTIONS = [
  PinDirection.Input,
  PinDirection.Output,
  PinDirection.Bidirectional,
  PinDirection.AnalogInput
] as const;

const VOLTAGE_LEVELS = [
  VoltageLevel.None,
  VoltageLevel.DC_12V,
  VoltageLevel.DC_24V,
  VoltageLevel.AC_220V,
  VoltageLevel.Signal_5V,
  VoltageLevel.Data
] as const;

export const templatePinDraftSchema = z.object({
  name: z.string().trim().min(1, 'Pin adı zorunlu'),
  // DB'de CHECK (0..1) var; burada yakalamak sunucuya gidip 400 ile dönmekten hızlı.
  relativeX: z.number().min(0, 'Konum x 0 ile 1 arasında olmalı').max(1, 'Konum x 0 ile 1 arasında olmalı'),
  relativeY: z.number().min(0, 'Konum y 0 ile 1 arasında olmalı').max(1, 'Konum y 0 ile 1 arasında olmalı'),
  side: enumValue(HANDLE_SIDES),
  channelNumber: z.number().int().nullable(),
  function: enumValue(PIN_FUNCTIONS),
  direction: enumValue(PIN_DIRECTIONS),
  voltageLevel: enumValue(VOLTAGE_LEVELS).nullable()
});

export const componentTemplateCreateSchema = z
  .object({
    name: z.string().trim().min(2, 'Şablon adı en az 2 karakter içermeli'),
    deviceTypeId: z.number().int(),
    width: z.number().gt(0, 'Genişlik sıfırdan büyük olmalı'),
    height: z.number().gt(0, 'Yükseklik sıfırdan büyük olmalı'),
    backgroundColor: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Arka plan rengi #RRGGBB biçiminde olmalı'),
    backgroundImageUrl: z.string().nullable(),
    // Boş bırakılabilir: pano çerçevesi gibi dekoratif bir şablonun pini olmayabilir.
    pins: z.array(templatePinDraftSchema).max(TEMPLATE_MAX_PINS, `Bir şablonda en fazla ${TEMPLATE_MAX_PINS} pin olabilir`)
  })
  .refine(v => new Set(v.pins.map(p => p.name.trim().toLocaleLowerCase('tr'))).size === v.pins.length, {
    // `IX_ComponentTemplatePin_ComponentTemplateId_Name` UNIQUE ve filtreli
    // DEĞİL. Burada yakalanmazsa sunucu 400 döner ve kullanıcı formda hangi
    // pinin sorunlu olduğunu göremez.
    message: 'Aynı şablonda iki pin aynı ada sahip olamaz',
    path: ['pins']
  });

export type TemplatePinDraft = z.infer<typeof templatePinDraftSchema>;
export type ComponentTemplateCreateRequest = z.infer<typeof componentTemplateCreateSchema>;
