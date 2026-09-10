import type { PointDto } from '@/models/diagram';

/**
 * Kablo yolu çizimi.
 *
 * Kayıtlı `waypoints` yalnızca ARA kırılma noktalarıdır; iki uç nokta React
 * Flow'un hesapladığı handle konumlarından gelir. Bu yüzden yol her zaman
 * `[source, ...waypoints, target]` olarak kurulur.
 */

/**
 * Noktalar arası düz parçalardan SVG path üretir.
 *
 * Kayıtlı noktalar kullanıcının koyduğu kırılma noktalarıdır — aralarına ek
 * köşe uydurulmaz. draw.io'nun davranışı da budur: bir noktayı taşıdığınızda
 * kablo tam oradan geçer, "akıllı" bir yeniden yönlendirme yapılmaz.
 */
export function buildWaypointPath(source: PointDto, waypoints: readonly PointDto[], target: PointDto): string {
  const points = [source, ...waypoints, target].filter(isFinitePoint);
  if (points.length < 2) return '';

  const [first, ...rest] = points as [PointDto, ...PointDto[]];
  return `M ${first.x},${first.y} ` + rest.map(p => `L ${p.x},${p.y}`).join(' ');
}

/**
 * Etiketin oturacağı nokta: yolun TOPLAM UZUNLUĞUNUN ortası.
 *
 * Noktaların aritmetik ortalaması değil — uzun bir parça ile kısa bir parçadan
 * oluşan bir kabloda ortalama, etiketi kablonun üstünde olmayan bir yere koyar.
 */
export function waypointPathMidpoint(source: PointDto, waypoints: readonly PointDto[], target: PointDto): PointDto {
  const points = [source, ...waypoints, target].filter(isFinitePoint);
  if (points.length === 0) return { x: 0, y: 0 };
  if (points.length === 1) return points[0]!;

  const segments = points.slice(1).map((point, index) => {
    const previous = points[index]!;
    return { from: previous, to: point, length: Math.hypot(point.x - previous.x, point.y - previous.y) };
  });

  const total = segments.reduce((sum, s) => sum + s.length, 0);
  if (total === 0) return points[0]!;

  let remaining = total / 2;
  for (const segment of segments) {
    if (remaining > segment.length) {
      remaining -= segment.length;
      continue;
    }
    const ratio = segment.length === 0 ? 0 : remaining / segment.length;
    return {
      x: segment.from.x + (segment.to.x - segment.from.x) * ratio,
      y: segment.from.y + (segment.to.y - segment.from.y) * ratio
    };
  }

  return points[points.length - 1]!;
}

/**
 * Sunucudan gelen bozuk bir nokta (NaN/Infinity) tüm path dizesini geçersiz
 * kılar ve kablo hiç çizilmez. Tek bir noktayı atmak, kabloyu kaybetmekten iyidir.
 */
function isFinitePoint(point: PointDto | undefined): point is PointDto {
  return !!point && Number.isFinite(point.x) && Number.isFinite(point.y);
}

// ──────────────────────────────────────────────────── kırılma düzenlemesi

/**
 * Her PARÇANIN orta noktası — "buraya kırılma ekle" düğmelerinin oturduğu yer.
 *
 * Dönen dizinin uzunluğu her zaman `waypoints.length + 1`: kırılma noktası
 * olmayan bir kabloda bile tek bir parça (uçtan uca) vardır ve ilk kırılma
 * oraya eklenir.
 *
 * İndeks, `insertWaypoint`'in `segmentIndex`'iyle AYNI şeydir — ikisi ayrı
 * numaralandırma kullansaydı, ortadaki düğmeye basmak kırılmayı başka bir yere
 * koyardı.
 */
export function segmentMidpoints(source: PointDto, waypoints: readonly PointDto[], target: PointDto): PointDto[] {
  const points = [source, ...waypoints, target];
  const midpoints: PointDto[] = [];

  for (let index = 0; index < points.length - 1; index++) {
    const from = points[index]!;
    const to = points[index + 1]!;
    if (!isFinitePoint(from) || !isFinitePoint(to)) continue;
    midpoints.push({ x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 });
  }

  return midpoints;
}

/**
 * `segmentIndex` numaralı parçayı ikiye böler.
 *
 * Yeni nokta tam o indekse yazılır: `[source, ...waypoints, target]` dizisinde
 * i numaralı parça `waypoints[i]`'den ÖNCE geldiği için, araya girmek demek o
 * indekse eklemek demektir.
 */
export function insertWaypoint(waypoints: readonly PointDto[], segmentIndex: number, point: PointDto): PointDto[] {
  const next = [...waypoints];
  // Sınır dışı bir indeks sessizce sona ekler. Kırılma eklemek geri
  // döndürülebilir bir işlem; kullanıcıya hata göstermeye değmez.
  const at = Math.min(Math.max(segmentIndex, 0), next.length);
  next.splice(at, 0, point);
  return next;
}

export function moveWaypoint(waypoints: readonly PointDto[], index: number, point: PointDto): PointDto[] {
  // Var olmayan indeks DEĞİŞTİRMEDEN döner: sürükleme sırasında kablo başka bir
  // yerden güncellenirse (ör. kaydetme sonrası tazeleme) indeks geçersizleşir ve
  // burada bir nokta uydurmak kabloyu bozardı.
  if (index < 0 || index >= waypoints.length) return [...waypoints];
  return waypoints.map((existing, i) => (i === index ? point : existing));
}

export function removeWaypoint(waypoints: readonly PointDto[], index: number): PointDto[] {
  if (index < 0 || index >= waypoints.length) return [...waypoints];
  return waypoints.filter((_, i) => i !== index);
}
