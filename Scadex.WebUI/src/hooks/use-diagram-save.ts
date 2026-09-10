import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { saveDiagram } from '@/api/diagram';
import type { DiagramJournal } from '@/lib/diagram/journal';
import type { DiagramSaveRequest } from '@/models/diagram';

/**
 * Kaydetme denetleyicisi.
 *
 * Bu hook grafı bilmez; ne gönderileceğini `buildRequest` söyler, sonucu
 * `onSuccess`/`onFailure` işler.
 */

export type SaveStatus = 'idle' | 'saving' | 'error';

export interface SaveController {
  status: SaveStatus;
  /**
   * Son başarılı gönderinin anı — ISO string, **istemcinin kendi saatinden**.
   *
   * Sunucudan gelmiyor ve gelmemeli: bu değer yalnızca `save-indicator`'daki
   * `HH:mm · kaydedildi` rozetini besliyor ve rozet `toLocaleTimeString` ile
   * kullanıcının yerel saatinde biçimleniyor. Sunucunun UTC'si kullanılsaydı,
   * iki saat kaydığında rozet kullanıcının duvar saatinden farklı bir değer
   * gösterirdi — oysa soru "ben bunu saat kaçta kaydettim".
   */
  lastSavedAt: string | null;
  errorMessage: string | null;
  /**
   * Bekleyen değişiklikleri gönderir. Kaydet düğmesi ve "kaydet ve çık" bunu
   * çağırır — başka hiçbir yerden tetiklenmez.
   */
  saveNow: () => Promise<void>;
}

export interface UseDiagramSaveParams {
  cabinetId: string;
  /**
   * Gönderilecek gövdeyi üretir ve günlüğü DEVRALIR.
   *
   * Çağıran taraf burada günlüğü takas eder: uçuş sırasında yapılan yeni
   * düzenlemeler taze bir günlüğe birikir, gönderilen kopya `sent` olarak geri
   * gelir ki hata durumunda geri katılabilsin.
   *
   * Gönderilecek bir şey yoksa `null` döner.
   */
  buildRequest: () => { request: DiagramSaveRequest; sent: DiagramJournal } | null;
  /**
   * `sent` başarıda da verilir: gövdede giden her kayıt artık sunucuda vardır ve
   * "kaydedilmemiş" defterinden düşürülmelidir (bkz. `lib/diagram/unsaved-store.ts`).
   */
  onSuccess: (sent: DiagramJournal) => void;
  onFailure: (sent: DiagramJournal, error: unknown) => void;
}

export function useDiagramSave({ cabinetId, buildRequest, onSuccess, onFailure }: UseDiagramSaveParams): SaveController {
  const [status, setStatus] = useState<SaveStatus>('idle');
  const [lastSavedAt, setLastSavedAt] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const inFlightRef = useRef(false);
  const unmountedRef = useRef(false);

  // Callback'ler her render'da yeniden üretiliyor; ref üzerinden okumak
  // `saveNow`'ın kimliğini sabit tutuyor. Aksi halde her render'da yeni bir
  // fonksiyon çıkar ve ona bağlı her effect yeniden kurulur.
  const buildRequestRef = useRef(buildRequest);
  const onSuccessRef = useRef(onSuccess);
  const onFailureRef = useRef(onFailure);

  /**
   * Tazeleme RENDER SIRASINDA değil, `useLayoutEffect`'te yapılır: render
   * sırasında ref yazmak React'in kurallarına aykırı (lint de reddediyor) —
   * bir render iptal edilirse ref, hiç commit edilmemiş bir dünyayı gösterir.
   */
  useLayoutEffect(() => {
    buildRequestRef.current = buildRequest;
    onSuccessRef.current = onSuccess;
    onFailureRef.current = onFailure;
  });

  const send = useCallback(
    async (request: DiagramSaveRequest, sent: DiagramJournal): Promise<void> => {
      // TEK deneme. Hız sınırı (429) da dahil hiçbir hata burada yeniden
      // denenmez: arka planda kendiliğinden çıkan ikinci bir istek, "yalnızca
      // düğmeye basınca gönderilir" kuralını sessizce deler.
      try {
        // Yanıt GÖVDESİZ: sunucudan öğrenilecek bir şey yok, 200'ün kendisi
        // "hepsi kalıcı" demek.
        await saveDiagram(cabinetId, request);
        if (unmountedRef.current) return;
        onSuccessRef.current(sent);
        setLastSavedAt(new Date().toISOString());
        setErrorMessage(null);
        setStatus('idle');
      } catch (error) {
        if (unmountedRef.current) return;
        // Sunucuda hiçbir şey değişmedi (transaction geri alındı); günlük geri
        // katılır ki bir sonraki deneme aynı değişiklikleri taşısın.
        onFailureRef.current(sent, error);
        setErrorMessage(error instanceof Error ? error.message : 'Kaydedilemedi');
        setStatus('error');
      }
    },
    [cabinetId]
  );

  const saveNow = useCallback(async (): Promise<void> => {
    // Tek uçuş. Sunucu "son yazan kazanır" ile çalıştığı için iki eşzamanlı
    // gönderi birbirini ezebilirdi; düğmeye iki kez basmak bunu tetiklerdi.
    //
    // Uçuş sırasındaki düzenlemeler için KUYRUK YOK: taze günlüğe birikirler ve
    // "kaydedilmemiş değişiklik" göstergesi açık kalır. Kullanıcı işi bitince
    // düğmeye yeniden basar — kaydetmenin tek tetikleyicisinin o düğme olması
    // zaten bu turun kararı.
    if (inFlightRef.current) return;

    inFlightRef.current = true;
    try {
      const payload = buildRequestRef.current();
      if (!payload) {
        setStatus(current => (current === 'error' ? current : 'idle'));
        return;
      }

      setStatus('saving');
      await send(payload.request, payload.sent);
    } finally {
      // `finally`: gönderi hata atsa bile bayrak düşmeli, yoksa editör bir daha
      // hiç kaydedemez — her `saveNow` "zaten uçuşta" deyip geri dönerdi.
      inFlightRef.current = false;
    }
  }, [send]);

  useEffect(() => {
    unmountedRef.current = false;
    return () => {
      unmountedRef.current = true;
    };
  }, []);

  return { status, lastSavedAt, errorMessage, saveNow };
}
