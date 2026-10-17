import { useLayoutEffect, useRef } from 'react'

type MarkerPosition = { left: number; top: number; width: number; height: number }
const easeOut = 'cubic-bezier(.22,1,.36,1)'

/** Motion adapted from the draft's usePortalMotion, without simulated navigation. */
export function useAccountMotion(pathname: string) {
  const rootRef = useRef<HTMLDivElement>(null)
  const lastPosition = useRef<MarkerPosition | null>(null)
  const cycle = useRef<{ path: string; from: MarkerPosition | null } | null>(null)

  useLayoutEffect(() => {
    const root = rootRef.current
    if (!root) return
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)')
    const animations: Animation[] = []
    const canAnimate = () => !reduced.matches && typeof root.animate === 'function'
    if (cycle.current?.path !== pathname) cycle.current = { path: pathname, from: lastPosition.current }
    const from = cycle.current.from
    const sidebar = root.querySelector<HTMLElement>('.account-sidebar')
    const marker = root.querySelector<HTMLElement>('.account-nav-marker')
    const selected = sidebar?.querySelector<HTMLElement>('[aria-current="page"]')
    let current: MarkerPosition | null = null
    let slide: Animation | undefined
    const align = (animate = false) => {
      if (!sidebar || !marker || !selected) return
      const link = selected.getBoundingClientRect()
      const bounds = sidebar.getBoundingClientRect()
      const next = { left: link.left - bounds.left + sidebar.scrollLeft,
        top: link.top - bounds.top + sidebar.scrollTop + (link.height - 20) / 2,
        width: bounds.width, height: bounds.height }
      if (!animate && current && Object.keys(next).every(key => next[key as keyof MarkerPosition] === current?.[key as keyof MarkerPosition])) return
      slide?.cancel()
      current = next
      lastPosition.current = next
      const transform = `translate(${next.left}px, ${next.top}px)`
      marker.style.transform = transform
      sidebar.dataset.motionNav = 'ready'
      if (animate && canAnimate() && from && from.width === next.width && from.height === next.height) {
        slide = marker.animate([{ transform: `translate(${from.left}px, ${from.top}px)` }, { transform }], { duration: 400, easing: easeOut })
        animations.push(slide)
      }
    }
    align(true)
    if (canAnimate()) {
      const reveal = (element: HTMLElement, delay: number, heading = false) => {
        animations.push(element.animate([
          { opacity: 0, transform: heading ? 'translate(10px, 8px)' : 'translateY(18px)' },
          { opacity: 1, transform: 'translate(0, 0)' },
        ], { duration: heading ? 360 : 440, delay, easing: easeOut, fill: 'backwards' }))
      }
      const heading = root.querySelector<HTMLElement>('.users-heading')
      if (heading) reveal(heading, 0, true)
      const cards = Array.from(root.querySelectorAll<HTMLElement>('.users-page .ant-card'))
        .filter(card => !card.parentElement?.closest('.ant-card') && card.getBoundingClientRect().top < window.innerHeight + 40)
      cards.slice(0, 8).forEach((card, index) => reveal(card, Math.min(45 + index * 32, 160)))
    }
    const settle = () => animations.forEach(animation => animation.cancel())
    const preferenceChanged = () => { if (reduced.matches) settle() }
    const realign = () => align()
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(realign)
    if (sidebar) observer?.observe(sidebar)
    if (selected) observer?.observe(selected)
    window.addEventListener('resize', realign)
    sidebar?.addEventListener('scroll', realign, { passive: true })
    reduced.addEventListener('change', preferenceChanged)
    root.addEventListener('pointerdown', settle)
    root.addEventListener('keydown', settle)
    return () => {
      settle()
      observer?.disconnect()
      window.removeEventListener('resize', realign)
      sidebar?.removeEventListener('scroll', realign)
      reduced.removeEventListener('change', preferenceChanged)
      root.removeEventListener('pointerdown', settle)
      root.removeEventListener('keydown', settle)
      if (sidebar) delete sidebar.dataset.motionNav
    }
  }, [pathname])
  return rootRef
}
