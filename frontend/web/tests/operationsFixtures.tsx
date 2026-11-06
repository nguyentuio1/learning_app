import type { ReactNode } from 'react'
import { vi } from 'vitest'
import { render, fireEvent, screen } from '@testing-library/react'
import { ConfigProvider } from 'antd'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '../src/contexts/authContext'
import type { Role } from '../src/types/auth'

export const operatorId = '11111111-1111-1111-1111-111111111111'
export const stationId = '22222222-2222-2222-2222-222222222222'
export const station = {
  id: stationId, stationCode: 'HCM-01', name: 'District 1 Hub', address: '1 Le Loi',
  latitude: 10.77, longitude: 106.7, status: 'OPERATIONAL', totalSlots: 4, emptySlots: 1,
  operatorId, operatorName: 'Station Operator', createdAt: '2026-11-06T00:00:00Z',
  updatedAt: '2026-11-06T01:00:00Z', slots: [], hostedDroneCount: 2,
}
export function authValue(role: Role = 'ADMIN') {
  return { status: 'authenticated' as const, user: { id: operatorId, email: 'fixture@example.com', fullName: 'Contract Test',
    phoneNumber: '0901234567', avatarUrl: null, status: 'ACTIVE', role: { id: 5, code: role, name: role }, createdAt: station.createdAt },
    accessToken: 'fixture', refreshToken: 'fixture', generation: 0, message: '', login: vi.fn(), register: vi.fn(), logout: vi.fn(), updateProfile: vi.fn() }
}
export function mount(children: ReactNode, role: Role = 'ADMIN', path = '/') {
  return render(<ConfigProvider theme={{ token: { motion: false } }}><AuthContext.Provider value={authValue(role)}>
    <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
  </AuthContext.Provider></ConfigProvider>)
}
export async function selectStation() {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Station' }))
  fireEvent.click(await screen.findByText('District 1 Hub · HCM-01'))
}
