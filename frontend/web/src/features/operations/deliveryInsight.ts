export const orderIdPattern = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i

export interface InsightRouteState {
  trackingNumber?: string
  reportSearch?: string
}
