interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_CLIENT_TYPE: string;
  /** Açık müşteri modülleri, virgülle ayrılmış (örn. `signalization`). Tanımsız = hiçbiri. */
  readonly VITE_MODULES?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
