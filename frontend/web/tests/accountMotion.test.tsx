import { afterEach, expect, it, vi } from 'vitest'
import { fireEvent, render } from '@testing-library/react'
import { useAccountMotion } from '../src/hooks/useAccountMotion'

function Screen({ route = '/admin/users' }: { route?: string }) {
  const ref = useAccountMotion(route)
  return <div ref={ref}>
    <aside className="account-sidebar"><span className="account-nav-marker" /><a href={route} aria-current="page">Current page</a></aside>
    <main className="users-page"><header className="users-heading">Users</header><div className="ant-card"><input aria-label="Search" /></div></main>
  </div>
}

const originalAnimate = Object.getOwnPropertyDescriptor(Element.prototype, 'animate')
function mockAnimate() {
  const cancel = vi.fn()
  const animate = vi.fn(() => ({ cancel }) as unknown as Animation)
  Object.defineProperty(Element.prototype, 'animate', { configurable: true, value: animate })
  return { animate, cancel }
}
afterEach(() => {
  vi.restoreAllMocks()
  if (originalAnimate) Object.defineProperty(Element.prototype, 'animate', originalAnimate)
  else Reflect.deleteProperty(Element.prototype, 'animate')
})

it('animates headings and cards without replaying on a same-route render', () => {
  const { animate, cancel } = mockAnimate()
  const screen = render(<Screen />)
  expect(animate).toHaveBeenCalledTimes(2)
  const input = screen.getByLabelText('Search')
  fireEvent.change(input, { target: { value: 'An' } })
  screen.rerender(<Screen />)
  expect(animate).toHaveBeenCalledTimes(2)
  expect(screen.getByLabelText('Search')).toBe(input)
  expect(input).toHaveValue('An')
  fireEvent.pointerDown(input)
  expect(cancel).toHaveBeenCalledTimes(2)
  screen.rerender(<Screen route="/admin/roles" />)
  expect(animate).toHaveBeenCalledTimes(5)
})

it('skips movement with reduced motion and still aligns the active marker', () => {
  vi.spyOn(window, 'matchMedia').mockReturnValue({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() } as unknown as MediaQueryList)
  const { animate } = mockAnimate()
  const { container } = render(<Screen />)
  expect(animate).not.toHaveBeenCalled()
  expect(container.querySelector('.account-sidebar')).toHaveAttribute('data-motion-nav', 'ready')
})
