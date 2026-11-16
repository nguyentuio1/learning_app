import { useEffect, useId, useRef, useState, type PointerEvent } from 'react'
import { Button } from 'antd'
import type { Position, TrackingStation } from './trackingApi'

type Coordinate = { latitude: number; longitude: number }
type Point = { x: number; y: number }
const tileSize = 256
const supported = (p: Coordinate) => Number.isFinite(p.latitude) && Math.abs(p.latitude) <= 85.05112878 && Number.isFinite(p.longitude) && Math.abs(p.longitude) <= 180
function project(p: Coordinate, zoom: number): Point {
  const scale = tileSize * 2 ** zoom, lat = p.latitude * Math.PI / 180
  return { x: (p.longitude + 180) / 360 * scale, y: (1 - Math.log(Math.tan(lat) + 1 / Math.cos(lat)) / Math.PI) / 2 * scale }
}
function nearest(longitude: number, reference: number) { return reference + ((longitude - reference + 540) % 360) - 180 }

// Adapted from the draft map; all markers now come from the canonical tracking DTO.
export function DeliveryMap({ origin, destination, position, stale }: { origin: TrackingStation; destination: TrackingStation; position: Position | null; stale: boolean }) {
  if (!supported(origin) || !supported(destination)) return <section className="dm-card dm-empty"><p>Station coordinates cannot be displayed on this basemap.</p></section>
  return <MapViewport origin={origin} destination={destination} position={position && supported(position) ? position : null} stale={stale}/>
}
function MapViewport({ origin, destination, position, stale }: { origin: TrackingStation; destination: TrackingStation; position: Position | null; stale: boolean }) {
  const host = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ width: 640, height: 450 })
  const [zoomDelta, setZoomDelta] = useState(0)
  const [pan, setPan] = useState<Point>({ x: 0, y: 0 })
  const drag = useRef<{ x: number; y: number; pan: Point } | null>(null)
  const [tiles, setTiles] = useState<Record<string, 'loaded' | 'failed'>>({})
  const [attempt, setAttempt] = useState(0)
  const [timeoutKey, setTimeoutKey] = useState('')
  const title = useId(), description = useId()
  useEffect(() => {
    const element = host.current
    if (!element) return
    const observer = new ResizeObserver(entries => { const r = entries[0]?.contentRect; if (r?.width && r.height) setSize({ width: r.width, height: r.height }) })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  const end = { ...destination, longitude: nearest(destination.longitude, origin.longitude) }
  let fitZoom = 16
  while (fitZoom > 2) {
    const a = project(origin, fitZoom), b = project(end, fitZoom)
    if (Math.abs(a.x - b.x) < Math.max(80, size.width - 100) && Math.abs(a.y - b.y) < Math.max(80, size.height - 120)) break
    fitZoom--
  }
  const zoom = Math.max(2, Math.min(18, fitZoom + zoomDelta))
  const center = project({ latitude: (origin.latitude + end.latitude) / 2, longitude: (origin.longitude + end.longitude) / 2 }, zoom)
  const offset = { x: center.x - size.width / 2 - pan.x, y: center.y - size.height / 2 - pan.y }
  const screen = (p: Coordinate) => { const v = project({ ...p, longitude: nearest(p.longitude, origin.longitude) }, zoom); return { x: v.x - offset.x, y: v.y - offset.y } }
  const a = screen(origin), b = screen(destination), drone = position ? screen(position) : null
  const visible: { key: string; x: number; y: number; url: string }[] = []
  const template = import.meta.env.VITE_MAP_TILE_URL || 'https://tile.openstreetmap.org/{z}/{x}/{y}.png'
  const world = 2 ** zoom
  for (let x = Math.floor(offset.x / tileSize); x <= Math.floor((offset.x + size.width) / tileSize); x++) {
    for (let y = Math.floor(offset.y / tileSize); y <= Math.floor((offset.y + size.height) / tileSize); y++) {
      if (y < 0 || y >= world) continue
      const wrappedX = ((x % world) + world) % world, key = `${zoom}/${wrappedX}/${y}`
      visible.push({ key, x: x * tileSize - offset.x, y: y * tileSize - offset.y, url: template.replace('{z}', String(zoom)).replace('{x}', String(wrappedX)).replace('{y}', String(y)) })
    }
  }
  const tileKey = visible.map(tile => tile.key).join('|') + ':' + attempt
  useEffect(() => { const timer = setTimeout(() => setTimeoutKey(tileKey), 12000); return () => clearTimeout(timer) }, [tileKey])
  const loading = visible.some(tile => !tiles[tile.key])
  const unavailable = visible.some(tile => tiles[tile.key] === 'failed') || (loading && timeoutKey === tileKey)
  const startDrag = (event: PointerEvent<SVGSVGElement>) => { if (event.button !== 0) return; drag.current = { x: event.clientX, y: event.clientY, pan }; event.currentTarget.setPointerCapture(event.pointerId) }
  const moveDrag = (event: PointerEvent<SVGSVGElement>) => { if (drag.current) setPan({ x: drag.current.pan.x + event.clientX - drag.current.x, y: drag.current.pan.y + event.clientY - drag.current.y }) }
  const pin = (point: Point, letter: string, color: string) => <g transform={`translate(${point.x},${point.y})`}><circle r="15" fill={color} stroke="white" strokeWidth="3"/><text fill="white" textAnchor="middle" dominantBaseline="central" fontSize="12" fontWeight="700">{letter}</text></g>
  function centerDrone() { if (position) { const p = project({ ...position, longitude: nearest(position.longitude, origin.longitude) }, zoom); setPan({ x: center.x - p.x, y: center.y - p.y }) } }
  return <section className="dm-map-card" aria-label="Delivery geographical map">
    <div className="dm-map-tools"><span><i className="dm-legend origin"/> Origin <i className="dm-legend destination"/> Destination <i className={`dm-legend ${stale ? 'stale' : 'drone'}`}/> {stale ? 'Last-known drone position' : 'Reported drone position'}</span><Button disabled={!position} onClick={centerDrone}>Center drone</Button></div>
    <div className="dm-canvas" ref={host}><svg width="100%" height="100%" viewBox={`0 0 ${size.width} ${size.height}`} role="img" aria-labelledby={`${title} ${description}`} tabIndex={0}
      onPointerDown={startDrag} onPointerMove={moveDrag} onPointerUp={() => { drag.current = null }} onPointerCancel={() => { drag.current = null }}
      onKeyDown={event => { const moves: Record<string, Point> = { ArrowLeft: { x: 60, y: 0 }, ArrowRight: { x: -60, y: 0 }, ArrowUp: { x: 0, y: 60 }, ArrowDown: { x: 0, y: -60 } }; const move = moves[event.key]; if (move) { event.preventDefault(); setPan(p => ({ x: p.x + move.x, y: p.y + move.y })) } }}>
      <title id={title}>{origin.name} to {destination.name}</title><desc id={description}>Station markers A and B. The dashed line is a display aid, not a verified flight path. {position ? stale ? 'Drone marker is the last-known position.' : 'Drone marker shows the latest reported coordinates.' : 'No drone location is displayed because no usable in-transit position is available.'} Use arrow keys to pan, or the zoom and fit buttons.</desc>
      <rect width={size.width} height={size.height} fill="#e7eedf"/>
      {visible.map(tile => <image key={`${tile.key}:${attempt}`} href={tile.url} x={tile.x} y={tile.y} width={256} height={256} onLoad={() => setTiles(all => ({ ...all, [tile.key]: 'loaded' }))} onError={() => setTiles(all => ({ ...all, [tile.key]: 'failed' }))}/>)}
      <path d={`M${a.x},${a.y} L${b.x},${b.y}`} fill="none" stroke="white" strokeWidth="7"/><path d={`M${a.x},${a.y} L${b.x},${b.y}`} fill="none" stroke="#638357" strokeDasharray="7 7" strokeWidth="3"/>
      {pin(a, 'A', '#1e4f3c')}{pin(b, 'B', '#947331')}
      {drone && <g aria-label="Reported drone marker" transform={`translate(${drone.x},${drone.y})`}><circle r="21" fill={stale ? '#996a28' : '#1e4f3c'} stroke="white" strokeWidth="3"/><g fill="none" stroke="white" strokeWidth="1.8" strokeLinecap="round"><rect x="-5" y="-5" width="10" height="10" rx="3"/><path d="m-5-5-5-5m15 5 5-5M-5 5l-5 5m15-5 5 5M-15-10h10M5-10h10M-15 10h10M5 10h10"/></g></g>}
    </svg><div className="dm-zoom"><Button aria-label="Zoom in" disabled={zoom === 18} onClick={() => { setZoomDelta(zoom - fitZoom + 1); setPan({ x: 0, y: 0 }) }}>+</Button><Button aria-label="Zoom out" disabled={zoom === 2} onClick={() => { setZoomDelta(zoom - fitZoom - 1); setPan({ x: 0, y: 0 }) }}>−</Button><Button aria-label="Fit stations" onClick={() => { setZoomDelta(0); setPan({ x: 0, y: 0 }) }}>⌖</Button></div>
    {loading && !unavailable && <span className="dm-layer-state" role="status">Loading basemap…</span>}
    {unavailable && <div className="dm-layer-state" role="status">Basemap tiles are unavailable. <Button size="small" onClick={() => { setTiles({}); setAttempt(v => v + 1) }}>Retry basemap</Button></div>}
    <span className="dm-attribution">© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap contributors</a></span></div>
    <p className="dm-disclaimer">Dashed line: a visual connection between stations, not a verified flight plan. Drone coordinates are never extrapolated.</p>
  </section>
}
