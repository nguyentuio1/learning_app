import { useEffect, useRef } from 'react'
import scene from '../../assets/images/login-scene.svg?raw'

export function LoginIllustration() {
  const container = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const drone = container.current?.querySelector<SVGGElement>('[data-drone]')
    const path = container.current?.querySelector<SVGPathElement>('[data-flight-path]')
    if (!drone || !path) return
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
    const length = path.getTotalLength()
    let frame = 0
    let elapsed = 0
    let last: number | null = null
    let reduceMotion = preference.matches
    const paint = () => {
      if (reduceMotion) { drone.removeAttribute('transform'); return }
      const progress = Math.min(elapsed / 5200, 1)
      const ease = progress * progress * (3 - 2 * progress)
      const point = path.getPointAtLength(ease * length)
      const scale = .5 + .5 * ease
      const hover = progress === 1 ? Math.sin((elapsed - 5200) / 850) * 2.5 : 0
      // The underside of the parcel tracks the visible green route, keeping
      // the aircraft above it as its perspective scale increases.
      drone.setAttribute('transform', `translate(${point.x} ${point.y + hover}) scale(${scale}) translate(-411 -488)`)
      drone.dataset.phase = progress < 1 ? 'takeoff' : 'hover'
      drone.dataset.progress = progress.toFixed(3)
    }
    const tick = (now: number) => {
      if (last !== null && !document.hidden) elapsed += Math.min(now - last, 60)
      last = now
      paint()
      frame = window.requestAnimationFrame(tick)
    }
    const onPreference = () => { reduceMotion = preference.matches; paint() }
    paint()
    preference.addEventListener('change', onPreference)
    frame = window.requestAnimationFrame(tick)
    return () => { window.cancelAnimationFrame(frame); preference.removeEventListener('change', onPreference) }
  }, [scene])
  return <section className="illustration-panel" aria-label="Drone delivery illustration">
    <div ref={container} className="scene" dangerouslySetInnerHTML={{ __html: scene }} />
  </section>
}
