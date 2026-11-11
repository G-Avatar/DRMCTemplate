import assert from 'node:assert/strict'
import { beforeEach, test } from 'node:test'
import { login } from '../auth.ts'

const visited: string[] = []
const storage = new Map<string, string>()
Object.assign(globalThis, {
  window: { location: { assign: (url: string) => visited.push(url) } },
  sessionStorage: {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => storage.set(key, value),
    removeItem: (key: string) => storage.delete(key),
  },
})

beforeEach(() => {
  visited.length = 0
  storage.clear()
})

test('sends the browser to the server login with the page to come back to', () => {
  login('/reports?year=2026')

  assert.deepEqual(visited, ['/api/account/login?returnUrl=%2Freports%3Fyear%3D2026'])
})

test('stops at the portal after three trips without a session, and lets the next visit try again', () => {
  for (let i = 0; i < 4; i++) login('/')

  assert.deepEqual(visited.slice(3), ['/api/account/portal'])
  assert.equal(visited.length, 4)
  assert.equal(storage.size, 0)
})
