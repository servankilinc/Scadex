import type { AppModule } from './types';
import { signalizationModule } from './signalization';

/**
 * Kurulumda AÇIK olan müşteri modülleri — `main.tsx` (rotalar), `app-sidebar.tsx` (menü) ve
 * `layouts/app.tsx` (layout eklentisi) yalnızca bu listeyi okur.
 *
 * Açma/kapama `VITE_MODULES` ile yapılır (virgülle ayrılmış adlar, örn. `signalization`). Bu, backend'deki
 * `Modules:<Ad>:Enabled` ayarının aynasıdır ve **ayrıca** ayarlanır: backend'de kapalı bir modülü burada açmak,
 * ekranların 404 almasıyla sonuçlanır.
 *
 * Yeni modül = `REGISTRY`'ye tek satır. Reflection/glob ile modül keşfi bilerek yapılmaz (backend kuralıyla aynı).
 */
const REGISTRY: AppModule[] = [signalizationModule];

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
