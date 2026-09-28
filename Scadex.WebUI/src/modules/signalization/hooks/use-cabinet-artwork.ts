import { useQuery } from '@tanstack/react-query';
import cabinetUrl from '@/assets/signalization/cabinet-inside.svg?url';
import cardReaderUrl from '@/assets/signalization/Card-Reader.svg?url';
import indoorClosedUrl from '@/assets/signalization/Indoor-Closed.svg?url';
import indoorOpenedUrl from '@/assets/signalization/Indoor-Opened.svg?url';
import ledClosedUrl from '@/assets/signalization/Led-Closed.svg?url';
import ledOpenedUrl from '@/assets/signalization/Led-Opened.svg?url';
import sirenClosedUrl from '@/assets/signalization/Siren-Closed.svg?url';
import sirenOpenedUrl from '@/assets/signalization/Siren-Opened.svg?url';
import { signalizationKeys } from '../api/query-keys';
import type { ArtworkSources, DeviceSymbol } from '../components/virtual-cabinet/artwork';

/** `artwork.ts > DEVICE_SYMBOLS` ile birebir: sembol → paketlenmiş dosyanın adresi. */
const DEVICE_URLS: Record<DeviceSymbol, string> = {
  'vc-led-on': ledOpenedUrl,
  'vc-led-off': ledClosedUrl,
  'vc-siren-on': sirenOpenedUrl,
  'vc-siren-off': sirenClosedUrl,
  'vc-indoor-open': indoorOpenedUrl,
  'vc-indoor-closed': indoorClosedUrl,
  'vc-card-reader': cardReaderUrl
};

/**
 * Sanal kabinin çizim dosyaları metin olarak: kasa + cihaz görünümleri. `?url` ile ayrı, önbelleklenen dosyalar olarak
 * paketlenir — `?raw` hepsini JS paketine gömerdi (`Card-Reader.svg` tek başına ~425 KB, çoğu gömülü PNG).
 *
 * İçerik bizim paketlediğimiz asset'tir, kullanıcı girdisi değildir; DOM'a olduğu gibi eklenmesi bu yüzden güvenlidir.
 */
export function useCabinetArtwork() {
  return useQuery({
    queryKey: signalizationKeys.cabinetArtwork(),
    queryFn: async (): Promise<ArtworkSources> => {
      const entries = Object.entries(DEVICE_URLS) as [DeviceSymbol, string][];
      const [cabinet, ...devices] = await Promise.all([fetchText(cabinetUrl), ...entries.map(([, url]) => fetchText(url))]);

      return {
        cabinet,
        devices: Object.fromEntries(entries.map(([symbol], index) => [symbol, devices[index]])) as Record<DeviceSymbol, string>
      };
    },
    // Paketlenmiş dosyalar oturum boyunca değişmez.
    staleTime: Infinity,
    gcTime: Infinity
  });
}

async function fetchText(url: string): Promise<string> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Kabin çizimi yüklenemedi: ${url} (HTTP ${response.status}).`);
  return response.text();
}
