import '@testing-library/jest-dom/vitest'
import { afterEach, vi } from 'vitest'
import { cleanup, configure } from '@testing-library/react'
afterEach(cleanup)
// findBy* and waitFor give up after 1 s by default, which is too short for a screen that loads for the first time on a slow CI runner (LA-327).
configure({ asyncUtilTimeout: 10000 })
Object.defineProperty(window, 'matchMedia', { writable: true, value: vi.fn().mockImplementation(query => ({
  matches: false, media: query, onchange: null, addListener: vi.fn(), removeListener: vi.fn(),
  addEventListener: vi.fn(), removeEventListener: vi.fn(), dispatchEvent: vi.fn(),
})) })
globalThis.ResizeObserver = class { observe() {} unobserve() {} disconnect() {} }
HTMLElement.prototype.scrollIntoView = vi.fn()
const getComputedStyle = window.getComputedStyle
window.getComputedStyle = element => getComputedStyle(element)
