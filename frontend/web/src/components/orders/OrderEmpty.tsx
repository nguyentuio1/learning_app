export function OrderEmpty({ title, description }: { title: string; description: string }) {
  return <div className="order-empty">
    <svg viewBox="0 0 32 32" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="m16 3 12 7v13l-12 7-12-7V10Zm0 14v13M4 10l12 7 12-7M10 6l12 7" /></svg>
    <h3>{title}</h3><p>{description}</p>
  </div>
}
