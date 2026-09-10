import { newId } from '@/lib/sequential-id';
import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';
import type { DiagramIoChannelDto, DiagramPinDto } from '@/models/diagram';

/**
 * Cihaz pinlerinin ve telemetri kanallarının İSTEMCİ TARAFINDAKİ üretimi.
 *
 * **Neden istemcide.** Diyagramdaki diğer bütün Guid'leri istemci üretiyor
 * (`lib/sequential-id.ts`); pin ve kanal bir zamanlar istisnaydı ve bedeli şuydu:
 * paletten bırakılan cihaz Kaydet'e basılana kadar pinsiz duruyor, dolayısıyla
 * kablolanamıyordu. Kimlik burada doğduğunda cihaz anında kablolanabilir hâle
 * gelir ve kaydetme sonrası graf tazelemeye gerek kalmaz.
 *
 * **Taşınan yalnızca kimlik.** Pinin adı, konumu, fonksiyonu, yönü ve gerilimi
 * gönderide GİTMEZ; sunucu her alanı `ComponentTemplatePin`'den kopyalar. Burada
 * üretilenler yalnızca canvas'ın o ana kadarki görüntüsüdür ve sunucunun
 * yazacağıyla birebir aynı olmalıdır — bu yüzden kopyalama tek bir yerde, bu
 * dosyada durur.
 */

export interface InstantiatedPins {
  pins: DiagramPinDto[];
  ioChannels: DiagramIoChannelDto[];
}

/** Paletten bırakma: şablonun pin şemasından yeni bir cihazın pinleri. */
export function instantiateFromTemplate(template: ComponentTemplatePaletteDto): InstantiatedPins {
  return instantiate(
    template.pins.map(pin => ({
      componentTemplatePinId: pin.id,
      name: pin.name,
      relativeX: pin.relativeX,
      relativeY: pin.relativeY,
      side: pin.side,
      function: pin.function,
      direction: pin.direction,
      voltageLevel: pin.voltageLevel,
      channelNumber: pin.channelNumber
    }))
  );
}

/**
 * Cihaz kopyalama: kaynağın pinlerinden kopyanın pinleri.
 *
 * Kaynak pinlerden türetiliyor, paletten değil — `duplicateDevice` böylece palet
 * cache'ine bağımlı olmuyor. Kaynağın pinleri şablonuyla her zaman uyumludur:
 * şablonun yalnızca oluşturma yolu var, pin şeması sonradan değiştirilemiyor.
 *
 * `componentTemplatePinId` KORUNUR (sunucu şemayla eşleşmeyi bununla doğrular),
 * Id'ler ise tazelenir — taşınsalardı kopyaya çizilen kablo kaynağa bağlanırdı.
 */
export function instantiateFromDevicePins(source: DiagramPinDto[]): InstantiatedPins {
  return instantiate(
    source.map(pin => ({
      componentTemplatePinId: pin.componentTemplatePinId,
      name: pin.name,
      relativeX: pin.relativeX,
      relativeY: pin.relativeY,
      side: pin.side,
      function: pin.function,
      direction: pin.direction,
      voltageLevel: pin.voltageLevel,
      channelNumber: pin.channelNumber
    }))
  );
}

type PinShape = Omit<DiagramPinDto, 'id' | 'ioChannelId'>;

function instantiate(shapes: PinShape[]): InstantiatedPins {
  // Aynı (yön, kanal numarası) ÇİFTİ tek bir IoChannel'dır — sunucudaki
  // `IX_IoChannel_CabinetId_Direction_ChannelNumber` bunu zorluyor. Yön anahtarın
  // parçası çünkü kartta IN1, AI1 ve OUT1 ÜÇ AYRI noktadır: çerçeve başlığı
  // dijital girişi 'I', analog girişi 'A', çıkışı 'O' ile ayırır ve bu üç kod
  // uzayı bağımsızdır.
  //
  // Şablonda iki pin aynı kanalı gösteriyorsa (ör. bir rölenin COM ve NO uçları)
  // ikisi de aynı kanala bağlanır. Kural sunucuda da var; buradaki kopyası ondan
  // sapmamalı, çünkü sapma bir UX pürüzü değil doğrudan 400 demek.
  const channelsByAddress = new Map<string, DiagramIoChannelDto>();
  const pins: DiagramPinDto[] = [];

  for (const shape of shapes) {
    let ioChannelId: string | null = null;

    if (shape.channelNumber != null) {
      const address = `${shape.direction}:${shape.channelNumber}`;
      let channel = channelsByAddress.get(address);
      if (!channel) {
        channel = {
          id: newId(),
          channelNumber: shape.channelNumber,
          direction: shape.direction,
          isEnabled: true,
          name: shape.name
        };
        channelsByAddress.set(address, channel);
      }
      ioChannelId = channel.id;
    }

    pins.push({ ...shape, id: newId(), ioChannelId });
  }

  return { pins, ioChannels: [...channelsByAddress.values()] };
}
