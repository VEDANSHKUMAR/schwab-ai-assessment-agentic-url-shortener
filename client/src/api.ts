const API_KEY_STORAGE = 'linkforge.apiKey'

export function getApiKey() {
  return localStorage.getItem(API_KEY_STORAGE) || 'dev-api-key-change-me'
}

export function setApiKey(key: string) {
  localStorage.setItem(API_KEY_STORAGE, key)
}

async function request<T>(path: string, init: RequestInit = {}, auth = false): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  if (auth) {
    headers.set('X-Api-Key', getApiKey())
  }

  const res = await fetch(path, { ...init, headers })
  if (!res.ok) {
    let detail = res.statusText
    try {
      const body = await res.json()
      detail = body.detail || body.title || detail
    } catch {
      /* ignore */
    }
    throw new Error(detail)
  }

  if (res.status === 204) {
    return undefined as T
  }

  return res.json() as Promise<T>
}

export type CreateResponse = {
  shortCode: string
  shortUrl: string
  originalUrl: string
  createdAt: string
  expiresAt?: string
}

export type AnalyticsResponse = {
  shortCode: string
  totalClicks: number
  clicksOverTime: { day: string; count: number }[]
  recentClicks: {
    occurredAt: string
    referrer?: string
    userAgent?: string
    deviceCategory?: string
  }[]
}

export type OrchestrationRun = {
  id: string
  scenario: string
  status: string
  brief: string
  createdAt: string
  updatedAt: string
  completedAt?: string
  failureReason?: string
  retryCount: number
  rollbackCount: number
  replanCount: number
  contextVersion: number
  endToEndLatencyMs?: number
  context: Record<string, string>
  stages: {
    id: string
    name: string
    status: string
    requiresApproval: boolean
    outputSummary?: string
    error?: string
    durationMs?: number
  }[]
  events: {
    at: string
    kind: string
    message: string
    stageId?: string
    decision?: string
  }[]
}

export const api = {
  createUrl: (body: { url: string; customAlias?: string; expiresAt?: string }) =>
    request<CreateResponse>('/api/v1/urls', { method: 'POST', body: JSON.stringify(body) }, true),

  getUrl: (shortCode: string) =>
    request<CreateResponse & { isActive: boolean; clickCount: number }>(
      `/api/v1/urls/${encodeURIComponent(shortCode)}`,
      {},
      true,
    ),

  disableUrl: (shortCode: string) =>
    request<void>(`/api/v1/urls/${encodeURIComponent(shortCode)}`, { method: 'DELETE' }, true),

  analytics: (shortCode: string) =>
    request<AnalyticsResponse>(
      `/api/v1/urls/${encodeURIComponent(shortCode)}/analytics`,
      {},
      true,
    ),

  startRun: (scenario: string, brief?: string) =>
    request<OrchestrationRun>('/api/v1/orchestration/runs', {
      method: 'POST',
      body: JSON.stringify({ scenario, brief }),
    }),

  getRun: (id: string) => request<OrchestrationRun>(`/api/v1/orchestration/runs/${id}`),

  approve: (id: string, approve: boolean, comment?: string) =>
    request<OrchestrationRun>(`/api/v1/orchestration/runs/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ approve, comment }),
    }),

  stop: (id: string) =>
    request<OrchestrationRun>(`/api/v1/orchestration/runs/${id}/stop`, { method: 'POST' }),

  replan: (
    id: string,
    body: {
      brief?: string
      changedArtifactKey?: string
      changedArtifactValue?: string
      reason?: string
    },
  ) =>
    request<OrchestrationRun>(`/api/v1/orchestration/runs/${id}/replan`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  metrics: () =>
    request<{
      totalRuns: number
      succeededRuns: number
      failedRuns: number
      retryCount: number
      rollbackCount: number
      replanCount: number
      successRate: number
      mttrMs: number
      averageEndToEndLatencyMs: number
      averageStageLatencyMs: number
    }>('/api/v1/orchestration/metrics'),
}
