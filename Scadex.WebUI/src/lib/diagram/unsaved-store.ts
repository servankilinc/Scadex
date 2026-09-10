import { useSyncExternalStore } from 'react';

/**
 * Henüz sunucuya yazılmamış kayıtların kimlikleri.
 *
 * **Neden var.** Guid'i artık istemci ürettiği için bir Id'ye bakarak "bu kayıt
 * sunucuda var mı" sorusunu cevaplamak MÜMKÜN DEĞİL — eski `tmp_` öneki tam da
 * bu işi görüyordu. Soru hâlâ gerçek: kaydedilmemiş bir cihaza kumanda
 * gönderilemez ve kumanda geçmişi sorgulanamaz (404'e giderdi).
 *
 * Öneki bir kayıt defteriyle değiştirmek, kimliğin kendisini anlam taşımaktan
 * kurtarır: Id sadece kimliktir, "kaydedildi mi" ayrı ve açık bir bilgidir.
 *
 * `live-store.ts` ile aynı desen — modül düzeyinde durum, `useSyncExternalStore`
 * ile okunur. React state'i DEĞİL: bu bilgi editörün graf state'ine ait değil ve
 * ona bağlanmayan bileşenlerden de sorulabiliyor olmalı.
 */

const unsavedIds = new Set<string>();
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

/** Canvas'ta doğan, henüz gönderilmemiş bir kayıt. */
export function markUnsaved(id: string): void {
  unsavedIds.add(id);
  emit();
}

/**
 * Gönderisi BAŞARIYLA tamamlanan kayıtlar. Kaydetme atomik olduğu için gövdede
 * giden her Id artık sunucuda vardır.
 */
export function markSaved(ids: Iterable<string>): void {
  let changed = false;
  for (const id of ids) changed = unsavedIds.delete(id) || changed;
  if (changed) emit();
}

/**
 * Hiç gönderilmeden silinen kayıt. Defterde bırakılırsa sızıntı olur —
 * editör oturumu boyunca büyüyen ölü bir küme.
 */
export function forgetUnsaved(id: string): void {
  if (unsavedIds.delete(id)) emit();
}

/** Editör kapanırken: defter oturuma değil, açık olan diyagrama aittir. */
export function resetUnsaved(): void {
  if (unsavedIds.size === 0) return;
  unsavedIds.clear();
  emit();
}

/**
 * Tepkisel OLMAYAN okuma — render dışından soranlar için.
 *
 * Gönderi gövdesi kurulurken kullanılır: pin ve kanal kimlikleri yalnızca YENİ
 * cihazlarda gider (mevcut bir cihaza gönderilirse sunucu 400 döner), ve "bu
 * cihaz yeni mi" sorusunun tek cevabı bu defter. Zamanlama önemli: gövde
 * `markSaved`'dan ÖNCE kurulur, dolayısıyla defter o an hâlâ doğrudur.
 */
export function isUnsaved(id: string): boolean {
  return unsavedIds.has(id);
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Tepkisel okuma: kayıt sunucuya yazıldığı anda bileşen yeniden render olur. */
export function useIsUnsaved(id: string | null | undefined): boolean {
  return useSyncExternalStore(
    subscribe,
    () => (id == null ? false : unsavedIds.has(id)),
    () => false
  );
}
