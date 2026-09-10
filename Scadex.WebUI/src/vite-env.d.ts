interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_CLIENT_TYPE: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
