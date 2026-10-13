import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'
import { api, getApiKey, setApiKey } from './api'
import type { AnalyticsResponse, CreateResponse, OrchestrationRun } from './api'

type Tab = 'shorten' | 'orchestration'

export default function App() {
  const [tab, setTab] = useState<Tab>('shorten')
  const [apiKey, setApiKeyState] = useState(getApiKey())

  return (
    <div className="app">
      <header className="hero">
        <h1 className="brand">
          Link<span>Forge</span>
        </h1>
        <p className="tagline">
          Production-style URL shortening with Redis-backed redirects, plus a governed agentic SDLC
          orchestrator for greenfield, brownfield, and ambiguous engineering scenarios.
        </p>
      </header>

      <nav className="nav" aria-label="Primary">
        <button className={tab === 'shorten' ? 'active' : ''} onClick={() => setTab('shorten')}>
          Shorten URLs
        </button>
        <button
          className={tab === 'orchestration' ? 'active' : ''}
          onClick={() => setTab('orchestration')}
        >
          Agentic Orchestration
        </button>
      </nav>

      {tab === 'shorten' ? (
        <ShortenPanel
          apiKey={apiKey}
          onApiKeyChange={(k) => {
            setApiKey(k)
            setApiKeyState(k)
          }}
        />
      ) : (
        <OrchestrationPanel />
      )}

      <p className="footer-note">
        Management APIs require <code>X-Api-Key</code>. Default local key:{' '}
        <code>dev-api-key-change-me</code>. Redirects are public.
      </p>
    </div>
  )
}

function ShortenPanel({
  apiKey,
  onApiKeyChange,
}: {
  apiKey: string
  onApiKeyChange: (v: string) => void
}) {
  const [url, setUrl] = useState('https://learn.microsoft.com/dotnet/')
  const [alias, setAlias] = useState('')
  const [expiresAt, setExpiresAt] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [created, setCreated] = useState<CreateResponse | null>(null)
  const [analytics, setAnalytics] = useState<AnalyticsResponse | null>(null)

  async function onCreate(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setAnalytics(null)
    try {
      const body: { url: string; customAlias?: string; expiresAt?: string } = { url }
      if (alias.trim()) body.customAlias = alias.trim()
      if (expiresAt) body.expiresAt = new Date(expiresAt).toISOString()
      const result = await api.createUrl(body)
      setCreated(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Create failed')
    } finally {
      setBusy(false)
    }
  }

  async function loadAnalytics() {
    if (!created) return
    setBusy(true)
    setError(null)
    try {
      setAnalytics(await api.analytics(created.shortCode))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Analytics failed')
    } finally {
      setBusy(false)
    }
  }

  async function disable() {
    if (!created) return
    setBusy(true)
    setError(null)
    try {
      await api.disableUrl(created.shortCode)
      setError(null)
      alert(`Disabled ${created.shortCode}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Disable failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="panel">
      <h2>Create a short link</h2>
      <p className="help">Validates scheme, blocks private hosts, supports aliases and expiration.</p>
      <form className="grid" onSubmit={onCreate}>
        <label>
          API key
          <input value={apiKey} onChange={(e) => onApiKeyChange(e.target.value)} />
        </label>
        <label>
          Destination URL
          <input value={url} onChange={(e) => setUrl(e.target.value)} required />
        </label>
        <div className="row two">
          <label>
            Custom alias (optional)
            <input value={alias} onChange={(e) => setAlias(e.target.value)} placeholder="docs-dotnet" />
          </label>
          <label>
            Expires at (optional)
            <input
              type="datetime-local"
              value={expiresAt}
              onChange={(e) => setExpiresAt(e.target.value)}
            />
          </label>
        </div>
        <div className="actions">
          <button className="btn btn-primary" disabled={busy} type="submit">
            {busy ? 'Working…' : 'Shorten'}
          </button>
        </div>
      </form>

      {error && <div className="error">{error}</div>}

      {created && (
        <div className="result">
          <div>
            Short URL:{' '}
            <a href={created.shortUrl} target="_blank" rel="noreferrer">
              <code>{created.shortUrl}</code>
            </a>
          </div>
          <div>
            Code: <code>{created.shortCode}</code>
          </div>
          <div className="actions">
            <button className="btn btn-ghost" type="button" onClick={loadAnalytics} disabled={busy}>
              Load analytics
            </button>
            <button className="btn btn-danger" type="button" onClick={disable} disabled={busy}>
              Disable
            </button>
          </div>
        </div>
      )}

      {analytics && (
        <div className="result">
          <strong>Total clicks:</strong> {analytics.totalClicks}
          <ul>
            {analytics.recentClicks.slice(0, 5).map((c) => (
              <li key={c.occurredAt}>
                {new Date(c.occurredAt).toLocaleString()} · {c.deviceCategory || 'unknown'}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

function OrchestrationPanel() {
  const [scenario, setScenario] = useState('Greenfield')
  const [brief, setBrief] = useState('')
  const [run, setRun] = useState<OrchestrationRun | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [metrics, setMetrics] = useState<string>('')

  useEffect(() => {
    api.metrics()
      .then((m) =>
        setMetrics(
          `Runs ${m.totalRuns} · success ${(m.successRate * 100).toFixed(0)}% · retries ${m.retryCount} · rollbacks ${m.rollbackCount} · replans ${m.replanCount} · MTTR ${m.mttrMs.toFixed(0)}ms · e2e ${m.averageEndToEndLatencyMs.toFixed(0)}ms`,
        ),
      )
      .catch(() => undefined)
  }, [run?.status])

  async function start() {
    setBusy(true)
    setError(null)
    try {
      const started = await api.startRun(scenario, brief || undefined)
      setRun(started)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Start failed')
    } finally {
      setBusy(false)
    }
  }

  async function approve(ok: boolean) {
    if (!run) return
    setBusy(true)
    setError(null)
    try {
      setRun(await api.approve(run.id, ok, ok ? 'Approved via LinkForge UI' : 'Rejected via UI'))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Approval failed')
    } finally {
      setBusy(false)
    }
  }

  async function stop() {
    if (!run) return
    setBusy(true)
    try {
      setRun(await api.stop(run.id))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Stop failed')
    } finally {
      setBusy(false)
    }
  }

  async function replan() {
    if (!run) return
    setBusy(true)
    setError(null)
    try {
      setRun(
        await api.replan(run.id, {
          brief: `${run.brief}\n\n[Replanned] stakeholder clarified scope.`,
          changedArtifactKey: 'requirements.json',
          changedArtifactValue: JSON.stringify({
            replanned: true,
            at: new Date().toISOString(),
            note: 'Upstream requirements changed; downstream stages invalidated',
          }),
          reason: 'Upstream requirements changed — demonstrate dynamic re-plan',
        }),
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Replan failed')
    } finally {
      setBusy(false)
    }
  }

  const waiting = run?.stages.some((s) => s.status === 'WaitingApproval')

  return (
    <section className="panel">
      <h2>Agentic SDLC orchestration</h2>
      <p className="help">
        Dependency graph with parallel design stages, human approval gates, bounded retries,
        safe-stop, and decision lineage. Stage agents are deterministic by default (no LLM key
        required).
      </p>

      <div className="grid">
        <label>
          Scenario
          <select value={scenario} onChange={(e) => setScenario(e.target.value)}>
            <option value="Greenfield">Greenfield</option>
            <option value="Brownfield">Brownfield</option>
            <option value="Ambiguous">Ambiguous</option>
          </select>
        </label>
        <label>
          Brief (optional override)
          <textarea
            rows={3}
            value={brief}
            onChange={(e) => setBrief(e.target.value)}
            placeholder="Leave blank to use the scenario default brief"
          />
        </label>
        <div className="actions">
          <button className="btn btn-primary" type="button" disabled={busy} onClick={start}>
            Start run
          </button>
          {waiting && (
            <>
              <button className="btn btn-primary" type="button" disabled={busy} onClick={() => approve(true)}>
                Approve gate
              </button>
              <button className="btn btn-warn" type="button" disabled={busy} onClick={() => approve(false)}>
                Reject
              </button>
            </>
          )}
          {run && run.status !== 'Succeeded' && run.status !== 'Stopped' && (
            <button className="btn btn-danger" type="button" disabled={busy} onClick={stop}>
              Safe stop
            </button>
          )}
          {run && (
            <button className="btn btn-ghost" type="button" disabled={busy} onClick={replan}>
              Re-plan (upstream change)
            </button>
          )}
        </div>
      </div>

      {metrics && <div className="meta">{metrics}</div>}
      {error && <div className="error">{error}</div>}

      {run && (
        <>
          <div className="meta">
            <span>
              Status: <strong>{run.status}</strong>
            </span>
            <span>Run: {run.id.slice(0, 8)}…</span>
            <span>Retries: {run.retryCount}</span>
            <span>Replans: {run.replanCount}</span>
            <span>Context v{run.contextVersion}</span>
          </div>
          <ul className="stages">
            {run.stages.map((s) => (
              <li key={s.id}>
                <span>
                  {s.name}
                  {s.requiresApproval ? ' ⊙' : ''}
                  {s.outputSummary ? ` — ${s.outputSummary}` : ''}
                </span>
                <span className={`badge ${s.status}`}>{s.status}</span>
              </li>
            ))}
          </ul>
          <div className="events">
            {run.events
              .slice()
              .reverse()
              .slice(0, 12)
              .map((e) => (
                <div key={`${e.at}-${e.kind}-${e.message}`}>
                  <strong>{e.kind}</strong>: {e.message}
                </div>
              ))}
          </div>
        </>
      )}
    </section>
  )
}
