import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { toast } from 'sonner';
import { useOpenSessions } from '../hooks/use-operator-sessions';
import { flagsOf, isAlertFlag } from '../models/enums';

/**
 * Canlı güvenlik uyarısı — layout'a BİR KEZ takılır (modül manifestosundaki `LayoutExtension`), kullanıcı hangi
 * sayfada olursa olsun çalışır.
 *
 * `/api/OperatorSession/open` ucunu 10 sn'de bir yoklar (SignalR olayı bilerek yok) ve güvenlik uyarısı
 * (`hasAlert`: kartsız giriş / zorla açma) taşıyan her açık oturum için **oturum başına bir kez** kalıcı bir
 * bildirim çıkarır. Onay akışı YOKTUR: bildirimi kapatmak yalnızca bildirimi kapatır, bayrak oturumda kalır ve
 * raporda görünür.
 *
 * Bilinen sınır: uyarılı bir oturum iki yoklama arasında açılıp kapanırsa bildirim çıkmaz (canlı liste yalnızca
 * açık oturumları taşır); kayıt raporda `flags` filtresiyle bulunur.
 */
export default function SessionAlertWatcher() {
  const navigate = useNavigate();
  const { data } = useOpenSessions();

  // Bildirimi çıkmış oturumlar. Ref: bu bir render girdisi değil, yalnızca yan etkinin hafızası. Sayfa
  // yenilenince sıfırlanır — hâlâ açık ve uyarılı oturum yeniden bildirilir, bu istenen davranış.
  const notifiedRef = useRef(new Set<number>());

  useEffect(() => {
    if (!data) return;

    for (const session of data) {
      if (!session.hasAlert || notifiedRef.current.has(session.id)) continue;
      notifiedRef.current.add(session.id);

      const reasons = flagsOf(session.flags)
        .filter(info => isAlertFlag(info.flag))
        .map(info => info.label)
        .join(', ');

      toast.warning(`Güvenlik uyarısı: ${reasons}`, {
        id: `signalization-alert-${session.id}`,
        description: `${session.cabinetName ?? 'Kabin'} · ${session.outerDoorName} (işlem #${session.id})`,
        // Kalıcı: kullanıcı kapatana ya da detaya gidene kadar ekranda kalır.
        duration: Infinity,
        closeButton: true,
        action: {
          label: 'Detay',
          onClick: () => navigate(`/signalization/sessions/${session.id}`)
        }
      });
    }
  }, [data, navigate]);

  return null;
}
