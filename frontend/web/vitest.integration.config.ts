import process from 'node:process'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Opt-in only: this suite creates/updates/deactivates users in a disposable API.
const target = process.env.LA94_API_URL
if (process.env.LA94_ALLOW_MUTATIONS !== '1' || !target ||
    !['127.0.0.1', 'localhost'].includes(new URL(target).hostname) ||
    !process.env.LA94_ADMIN_EMAIL || !process.env.LA94_ADMIN_PASSWORD) {
  throw new Error('Set LA94_ALLOW_MUTATIONS=1, LA94_API_URL (localhost only), LA94_ADMIN_EMAIL and LA94_ADMIN_PASSWORD for a disposable LA-92 database.')
}

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    include: ['integration/**/*.test.tsx'],
    testTimeout: 60000,
    hookTimeout: 30000,
    fileParallelism: false,
  },
})
