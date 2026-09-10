// Commands — kullanici girdisi, zod ile dogrulanir
export { canvasSettingsUpsertSchema, type CanvasSettingsUpsertRequest } from './commands/canvasSettingsUpsertRequest';

// Query yok: canvas ayarlari OKUMASI diyagram aggregate'inin icinde geliyor
// (`DiagramCanvasSettingsDto`, bkz. @/models/diagram). Ayri bir okuma tipi
// acmak ayni veriyi iki sekle bolmek olurdu.
