import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom', setupFiles: ['./tests/setup.ts'], include: ['tests/**/*.test.{ts,tsx}'], testTimeout: 15000,
    // Calendar accessibility queries are CPU-heavy in JSDOM. Keep the small CI
    // runner from oversubscribing workers; retain all assertions and timeouts.
    maxWorkers: process.env.CI ? 1 : undefined,
  },
})
