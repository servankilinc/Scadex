import type { AppModule, AutomationIdsQueryOptions } from './types';
import { cameraManagementModule } from './cameramanagement';
import { remoteDeskModule } from './remotedesk';
import { signalizationModule } from './signalization';

/**
 * Kurulumda AÇIK olan modüller ve çözümler — `main.tsx` (rotalar), `app-sidebar.tsx` (menü) ve
 * `layouts/app.tsx` (layout eklentisi) yalnızca bu listeyi okur.
 *
 * Açma/kapama `VITE_MODULES` ile yapılır (virgülle ayrılmış adlar, örn. `signalization,remotedesk`). Bu, backend'deki
 * `Modules:<Ad>:Enabled` ayarının aynasıdır ve **ayrıca** ayarlanır: backend'de kapalı bir modülü burada açmak,
 * ekranların 404 almasıyla sonuçlanır.
 *
 * Yeni modül = `REGISTRY`'ye tek satır. Reflection/glob ile modül keşfi bilerek yapılmaz (backend kuralıyla aynı).
 */
// Yetenek modülleri önce, çözümler sonra (menü sırası). Çözüm bağımlı olduğu modülü ayrıca açtırır: signalization → cameramanagement.
const REGISTRY: AppModule[] = [cameraManagementModule, remoteDeskModule, signalizationModule];

function parseModuleKeys(raw: string | undefined): Set<string> {
  return new Set(
    (raw ?? '')
      .split(',')
      .map(key => key.trim().toLowerCase())
      .filter(Boolean)
  );
}

const enabledKeys = parseModuleKeys(import.meta.env.VITE_MODULES);

export const enabledModules: AppModule[] = REGISTRY.filter(module => enabledKeys.has(module.key));

/** Açık modüllerin "işlem yapılan otomasyon" sorguları — ana sayfa haritası okur. Statik: liste çalışırken değişmez. */
export const busyAutomationQueries: AutomationIdsQueryOptions[] = enabledModules.flatMap(module =>
  module.busyAutomationsQuery ? [module.busyAutomationsQuery] : []
);

/** Açık modüllerin "alarm olan otomasyon" sorguları — ana sayfa haritası okur. Statik: liste çalışırken değişmez. */
export const alertAutomationQueries: AutomationIdsQueryOptions[] = enabledModules.flatMap(module =>
  module.alertAutomationsQuery ? [module.alertAutomationsQuery] : []
);

/** Açık modüllerin otomasyon detay paneli bölümleri — ana sayfa paneli okur. Statik: liste çalışırken değişmez. */
export const automationPanelSections = enabledModules.flatMap(module =>
  module.AutomationPanelSection ? [{ key: module.key, Component: module.AutomationPanelSection }] : []
);

/** Açık modüllerin otomasyon detay paneli EN ÜST eylemleri — ana sayfa paneli okur. Statik: liste çalışırken değişmez. */
export const automationPanelTopActions = enabledModules.flatMap(module =>
  module.AutomationPanelTopAction ? [{ key: module.key, Component: module.AutomationPanelTopAction }] : []
);
