import { useEffect, useRef, useState, type ReactNode } from 'react'

// A lightweight adaptation of the fade-on-entry interaction in React Bits Fade Content.
export function OrderReveal({ children, className = '' }: { children: ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null)
  const [visible, setVisible] = useState(false)
  useEffect(() => {
    const element = ref.current
    if (!element || typeof IntersectionObserver === 'undefined') return
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) { setVisible(true); observer.disconnect() }
    }, { threshold: 0.08 })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  return <div ref={ref} className={`order-reveal ${visible ? 'order-revealed' : ''} ${className}`}>{children}</div>
}
