import axios from 'axios'

export function customerAddressIssue(error: unknown, fallback: string) {
  if (!axios.isAxiosError(error)) return error instanceof Error ? error.message : fallback
  const data = error.response?.data as { detail?: string; title?: string; errors?: Record<string, string[]> } | undefined
  const fields = data?.errors && Object.values(data.errors).flat().filter(Boolean)
  return fields?.join(' ') || data?.detail || data?.title || fallback
}
