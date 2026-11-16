import { useEffect, useState, type ReactNode } from 'react'
import { Button, Drawer } from 'antd'
import './account-mobile-navigation.css'

// Enabled only by features that opt in through their navigation metadata.
export function AccountMobileNavigation({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false)
  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 761px)')
    function onResize() { if (desktop.matches) setOpen(false) }
    desktop.addEventListener('change', onResize)
    return () => desktop.removeEventListener('change', onResize)
  }, [])
  return <>
    <Button className="account-mobile-menu-toggle" aria-label="Open navigation menu" aria-expanded={open} onClick={() => setOpen(true)}>
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16"/></svg>
    </Button>
    <Drawer title="Workspace" placement="left" open={open} onClose={() => setOpen(false)} rootClassName="account-mobile-menu"
      styles={{ wrapper: { width: 'min(320px, 88vw)' }, body: { padding: 0 }, header: { background: '#e6eedc', borderColor: '#c5d3bc', color: '#203b30' } }}>
      <div className="account-sidebar account-mobile-menu-content" onClick={event => {
        if (event.target instanceof Element && event.target.closest('a, button')) setOpen(false)
      }}>{children}</div>
    </Drawer>
  </>
}
