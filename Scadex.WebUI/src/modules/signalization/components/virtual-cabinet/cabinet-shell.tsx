import type { ReactNode } from 'react';
import { CAMERA_IMAGE_HREF, SIREN_IMAGE_HREF } from './figure-assets';

/**
 * Kabinin gövdesi — `src/assets/signalization/cabinet-inside.svg`'nin kod karşılığı. Asset dosyası tasarım kaynağı
 * olarak yerinde durur; burası onun **cihazsız** hâlidir: SVG'deki dört `<g id="…-Box">` yer tutucusu silindi, yerlerine
 * slot geldi. (`<img src="*.svg">` ile gelen bir SVG'ye prop geçilemez, projede `vite-plugin-svgr` de yok.)
 *
 * Bütün `<defs>` BURADA toplanır: kimlikler belge geneline yayılır, her cihaz bileşeni kendi kopyasını taşısaydı ikinci
 * bir `Indoor` çizildiği an kimlikler çakışırdı. Asset'lerden gelen Figma kimlikleri (`paint0_radial_55_522`,
 * `clip0_55_452`, …) kullanılmaz; hepsi `vc-` önekiyle yeniden adlandırıldı.
 */
interface CabinetShellProps {
  /** Sol üstteki kamera (x 200–270, y 75–140). */
  camera?: ReactNode;
  /** Orta üstteki aydınlatma LED'i — `translate(350, 71)` bekler. */
  led?: ReactNode;
  /** Sağ üstteki siren — açıkken `translate(543, 85)`, kapalıyken `translate(545, 85)`. */
  siren?: ReactNode;
  /** İç kapı ızgarası (x 210–590, y 230–690). */
  indoors?: ReactNode;
  className?: string;
}

export function CabinetShell({ camera, led, siren, indoors, className }: CabinetShellProps) {
  return (
    <svg viewBox='0 0 800 800' fill='none' xmlns='http://www.w3.org/2000/svg' className={className} role='img' aria-label='Kabin iç görünümü'>
      {/* ── Kasa gövdesi ve iç yüzey ────────────────────────────────────── */}
      <path
        d='M656 40H144C141.791 40 140 41.7909 140 44V756C140 758.209 141.791 760 144 760H656C658.209 760 660 758.209 660 756V44C660 41.7909 658.209 40 656 40Z'
        fill='url(#vc-body)'
        stroke='#4A5568'
        strokeWidth='2'
      />
      <path d='M640 760H160V780H640V760Z' fill='#2D3748' />
      <path d='M160 60L200 100H600L640 60H160Z' fill='#2D3748' />
      <path d='M160 740L200 700H600L640 740H160Z' fill='#2D3748' />
      <path d='M160 60L200 100V700L160 740V60Z' fill='#4A5568' />
      <path d='M640 60L600 100V700L640 740V60Z' fill='#2D3748' />
      <path d='M600 100H200V700H600V100Z' fill='#404B5C' />
 
      {/* ── Cihazlar ────────────────────────────────────────────────────── */}
      {camera}

      {/* LED'in arkasındaki tabla: cihazın kendisi değil, kasanın parçası. */}
      <g filter='url(#vc-led-plate-shadow)'>
        <rect x='333' y='54' width='150' height='150' rx='16' fill='url(#vc-led-plate)' fillOpacity='0.25' shapeRendering='crispEdges' />
      </g>
      {led}
      {siren}
      {indoors}

      {/* ── Açık yan kapaklar (cihazların ÜSTÜNDE çizilir) ──────────────── */}
      <path d='M40 20L140 40V760L40 780V20Z' fill='url(#vc-door-left)' stroke='#4A5568' strokeWidth='2' />
      <path d='M50 35L130 50V750L50 765V35Z' stroke='#A0AEC0' strokeWidth='1.5' />
      <path
        d='M143 150H137C135.895 150 135 150.895 135 152V188C135 189.105 135.895 190 137 190H143C144.105 190 145 189.105 145 188V152C145 150.895 144.105 150 143 150Z'
        fill='#CBD5E0'
        stroke='#4A5568'
      />
      <path
        d='M143 610H137C135.895 610 135 610.895 135 612V648C135 649.105 135.895 650 137 650H143C144.105 650 145 649.105 145 648V612C145 610.895 144.105 610 143 610Z'
        fill='#CBD5E0'
        stroke='#4A5568'
      />
      <path d='M760 20L660 40V760L760 780V20Z' fill='url(#vc-door-right)' stroke='#4A5568' strokeWidth='2' />
      <path d='M750 35L670 50V750L750 765V35Z' stroke='#A0AEC0' strokeWidth='1.5' />
      <path
        d='M663 150H657C655.895 150 655 150.895 655 152V188C655 189.105 655.895 190 657 190H663C664.105 190 665 189.105 665 188V152C665 150.895 664.105 150 663 150Z'
        fill='#CBD5E0'
        stroke='#4A5568'
      />
      <path
        d='M663 610H657C655.895 610 655 610.895 655 612V648C655 649.105 655.895 650 657 650H663C664.105 650 665 649.105 665 648V612C665 610.895 664.105 610 663 610Z'
        fill='#CBD5E0'
        stroke='#4A5568'
      />

      <defs>
        {/* Fırçalanmış çelik: düz 3 duraklı gradyan yerine art arda açık/koyu bantlar —
            metal yüzeyde ışığın farklı açılarda yansımasını taklit eder. */}
        <linearGradient id='vc-body' x1='140' y1='40' x2='660' y2='40' gradientUnits='userSpaceOnUse'>
          <stop offset='0' stopColor='#5E6B7D' />
          <stop offset='0.12' stopColor='#8996A8' />
          <stop offset='0.22' stopColor='#6B7889' />
          <stop offset='0.38' stopColor='#AEB9C8' />
          <stop offset='0.5' stopColor='#C7D0DC' />
          <stop offset='0.62' stopColor='#9CA9BB' />
          <stop offset='0.78' stopColor='#7D8A9C' />
          <stop offset='0.9' stopColor='#9AA7B9' />
          <stop offset='1' stopColor='#5E6B7D' />
        </linearGradient>

        {/* Gövdenin ana hatlarına klonlanmış clip: parlaklık şeridinin gövde dışına taşmaması için. */}
        <clipPath id='vc-body-clip'>
          <path d='M656 40H144C141.791 40 140 41.7909 140 44V756C140 758.209 141.791 760 144 760H656C658.209 760 660 758.209 660 756V44C660 41.7909 658.209 40 656 40Z' />
        </clipPath>

        {/* İki diyagonal ışık şeridi — cam/parlak metal yüzeylerde görülen tipik yansıma deseni. */}
        <linearGradient id='vc-sheen' x1='140' y1='40' x2='660' y2='760' gradientUnits='userSpaceOnUse'>
          <stop offset='0' stopColor='white' stopOpacity='0' />
          <stop offset='0.32' stopColor='white' stopOpacity='0' />
          <stop offset='0.4' stopColor='white' stopOpacity='0.3' />
          <stop offset='0.48' stopColor='white' stopOpacity='0' />
          <stop offset='0.6' stopColor='white' stopOpacity='0' />
          <stop offset='0.66' stopColor='white' stopOpacity='0.16' />
          <stop offset='0.73' stopColor='white' stopOpacity='0' />
          <stop offset='1' stopColor='white' stopOpacity='0' />
        </linearGradient>

        {/* İç panel: tek düz renk yerine hafif üst-alt gradyanı — derinlik hissi verir. */}
        <linearGradient id='vc-panel' x1='200' y1='100' x2='200' y2='700' gradientUnits='userSpaceOnUse'>
          <stop offset='0' stopColor='#333D4D' />
          <stop offset='0.5' stopColor='#434F63' />
          <stop offset='1' stopColor='#333D4D' />
        </linearGradient>

        {/* Vida başlığı: merkezi ışık noktası + koyu kenar, gömme vida görünümü. */}
        <radialGradient id='vc-screw' cx='0.35' cy='0.32' r='0.8'>
          <stop offset='0' stopColor='#E7ECF3' />
          <stop offset='0.5' stopColor='#94A3B8' />
          <stop offset='1' stopColor='#333E4F' />
        </radialGradient>

        <linearGradient id='vc-door-left' x1='40' y1='20' x2='140' y2='20' gradientUnits='userSpaceOnUse'>
          <stop offset='0' stopColor='#5A6779' />
          <stop offset='0.35' stopColor='#7B889B' />
          <stop offset='0.55' stopColor='#98A5B7' />
          <stop offset='0.75' stopColor='#7B889B' />
          <stop offset='1' stopColor='#8895A7' />
        </linearGradient>

        <linearGradient id='vc-door-right' x1='660' y1='20' x2='760' y2='20' gradientUnits='userSpaceOnUse'>
          <stop offset='0' stopColor='#8895A7' />
          <stop offset='0.25' stopColor='#7B889B' />
          <stop offset='0.45' stopColor='#98A5B7' />
          <stop offset='0.65' stopColor='#7B889B' />
          <stop offset='1' stopColor='#5A6779' />
        </linearGradient>

        <linearGradient id='vc-led-plate' x1='408' y1='54' x2='408' y2='204' gradientUnits='userSpaceOnUse'>
          <stop offset='0.25' stopColor='#5C5C5C' stopOpacity='0' />
          <stop offset='1' stopColor='#707070' />
        </linearGradient>

        {/* LED yanarken çevresine vuran ışık; sönükken bu gradyan hiç kullanılmaz. */}
        <radialGradient id='vc-led-glow' cx='0' cy='0' r='1' gradientUnits='userSpaceOnUse' gradientTransform='translate(57.5 57.5) scale(57.5)'>
          <stop stopColor='#FDF08A' stopOpacity='0.8' />
          <stop offset='0.6' stopColor='#FDF08A' stopOpacity='0.4' />
          <stop offset='1' stopColor='#FDF08A' stopOpacity='0' />
        </radialGradient>

        <filter id='vc-led-plate-shadow' x='329' y='54' width='158' height='158' filterUnits='userSpaceOnUse' colorInterpolationFilters='sRGB'>
          <feFlood floodOpacity='0' result='BackgroundImageFix' />
          <feColorMatrix in='SourceAlpha' type='matrix' values='0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 127 0' result='hardAlpha' />
          <feOffset dy='4' />
          <feGaussianBlur stdDeviation='2' />
          <feComposite in2='hardAlpha' operator='out' />
          <feColorMatrix type='matrix' values='0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0.25 0' />
          <feBlend mode='normal' in2='BackgroundImageFix' result='effect1_dropShadow' />
          <feBlend mode='normal' in='SourceGraphic' in2='effect1_dropShadow' result='shape' />
        </filter>

        <pattern id='vc-camera-pattern' patternContentUnits='objectBoundingBox' width='1' height='1'>
          <use href='#vc-camera-image' transform='matrix(0.00533333 0 0 0.00553246 -0.1 -0.0763718)' />
        </pattern>
        <image id='vc-camera-image' width='225' height='225' preserveAspectRatio='none' href={CAMERA_IMAGE_HREF} />

        {/* Siren dokusu: açık ve kapalı gövde AYNI görseli kullanır, tek kopya yeter. */}
        <pattern id='vc-siren-pattern' patternContentUnits='objectBoundingBox' width='1' height='1'>
          <use href='#vc-siren-image' transform='matrix(0 -0.00833333 0.00996976 0 -0.738476 1.4375)' />
        </pattern>
        <image id='vc-siren-image' width='225' height='225' preserveAspectRatio='none' href={SIREN_IMAGE_HREF} />

        {/*
          Asset'teki üç ayrı clip yerine TEK tanım: clipPath kendi `<g transform>`'unun yerel uzayında çalışır, bu yüzden
          kaç iç kapı çizilirse çizilsin hepsi aynı 140x196'lık maskeyi paylaşabilir.
        */}
        <clipPath id='vc-indoor-clip'>
          <rect width='140' height='196' rx='4' fill='white' />
        </clipPath>
      </defs>
    </svg>
  );
}
