/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_AI_ASSISTANT_SEND_ENABLED?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
