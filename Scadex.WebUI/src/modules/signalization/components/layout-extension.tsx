import { useSessionRealtime } from '../hooks/use-session-realtime';
import SessionAlertWatcher from './session-alert-watcher';

/**
 * Modülün layout eklentisi — oturum açık olduğu sürece her sayfada:
 * - operatör işlemi canlı yayını → sorgu tazeleme (harita, canlı panel, geçmiş, detay),
 * - kartsız giriş / zorla açma bildirimi.
 */
export default function SignalizationLayoutExtension() {
  useSessionRealtime();
  return <SessionAlertWatcher />;
}
