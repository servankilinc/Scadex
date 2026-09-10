/**
 * Editörün değişiklik günlüğü.
 *
 * **Günlük yalnızca KİMLİK tutar, veri tutmaz.** Grafın güncel hali React Flow
 * state'inde durur (her node kendi DTO'sunu `data` içinde taşır); burada sadece
 * "hangi kayda dokunuldu / hangisi silindi" bilgisi var. Gönderi anında istek
 * gövdesi RF state'inden okunarak kurulur.
 *
 * Alternatif — isteği artımlı biriktirmek — aynı node on kez sürüklendiğinde on
 * ayrı taslak üretir ve hangisinin güncel olduğunu takip etmeyi gerektirirdi.
 * Kimlik tutmak bu sorunu tanım gereği ortadan kaldırır.
 *
 * **Oluşturma ile güncelleme AYRILMAZ.** Guid'i istemci ürettiği için ikisi de
 * "bu Id'nin son hâlini gönder" demektir; hangisi olduğuna sunucu, Id'yi
 * veritabanında arayarak karar verir.
 */

export type JournalFamily = 'devices' | 'connections' | 'annotations';

export interface FamilyJournal {
  /** Oluşturulan VEYA değiştirilen kayıtlar. */
  touched: Set<string>;
  deleted: Set<string>;
}

export type DiagramJournal = Record<JournalFamily, FamilyJournal>;

const FAMILIES: JournalFamily[] = ['devices', 'connections', 'annotations'];

export function createJournal(): DiagramJournal {
  return {
    devices: emptyFamily(),
    connections: emptyFamily(),
    annotations: emptyFamily()
  };
}

function emptyFamily(): FamilyJournal {
  return { touched: new Set(), deleted: new Set() };
}

export function isJournalEmpty(journal: DiagramJournal): boolean {
  return journalSize(journal) === 0;
}

export function journalSize(journal: DiagramJournal): number {
  return FAMILIES.reduce((sum, f) => sum + journal[f].touched.size + journal[f].deleted.size, 0);
}

export function markTouched(journal: DiagramJournal, family: JournalFamily, id: string): void {
  journal[family].touched.add(id);
}

/**
 * Silme, o kayda ait önceki tüm yazma niyetlerini yutar.
 *
 * Hiç kaydedilmemiş bir kayıt da silme listesine GİRER: sunucu karşılığı
 * bulunmayan Id'yi sessizce atlar. İstemcinin "bu kayıt gitti mi" bilgisini
 * taşıması gerekmemesinin sebebi budur.
 */
export function markDeleted(journal: DiagramJournal, family: JournalFamily, id: string): void {
  const entry = journal[family];
  entry.touched.delete(id);
  entry.deleted.add(id);
}

/**
 * Başarısız bir gönderiyi geri katar. Sunucuda hiçbir şey değişmediği için
 * birleşim kural olarak doğrudur; silme yazmayı yutmaya devam eder.
 */
export function mergeJournal(target: DiagramJournal, incoming: DiagramJournal): void {
  for (const family of FAMILIES) {
    const to = target[family];
    const from = incoming[family];

    for (const id of from.touched) {
      if (to.deleted.has(id)) continue;
      to.touched.add(id);
    }
    for (const id of from.deleted) {
      to.touched.delete(id);
      to.deleted.add(id);
    }
  }
}

/** Gönderilen günlükteki tüm yazma kimlikleri — kaydetme sonrası "artık kalıcı" işareti için. */
export function touchedIds(journal: DiagramJournal): string[] {
  return FAMILIES.flatMap(family => [...journal[family].touched]);
}
