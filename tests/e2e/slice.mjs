import { chromium } from 'playwright'

const BASE = 'http://localhost:5173'
const TOTAL_CHECKS = 4
const results = []
const log = (name, ok, detail = '') => {
  results.push({ name, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  - ' + detail : ''}`)
}

const browser = await chromium.launch({ headless: true })
const page = await browser.newPage()

let completed = false
try {
  await page.goto(BASE + '/customers', { waitUntil: 'domcontentloaded' })
  await page.waitForURL(/\/login/)
  log('Unauthenticated route redirects to login', /returnUrl/.test(page.url()), page.url())

  await page.fill('input[name="Username"]', 'admin')
  await page.fill('input[name="Password"]', 'wrongpass')
  await page.click('button[type="submit"]')
  await page.waitForSelector('.validation-summary')
  // .validation-summary also renders auth.ts's generic "Sign-in failed. Please
  // try again." fallback (e.g. on a 500), so asserting the element merely
  // exists would pass on that fallback too. Assert the actual text so only the
  // real "invalid credentials" response satisfies this check.
  const errorText = await page.textContent('.validation-summary')
  log('Wrong password shows error', errorText?.includes('Invalid username or password') ?? false, errorText ?? '')

  await page.fill('input[name="Username"]', 'admin')
  await page.fill('input[name="Password"]', 'admin123')
  await page.click('button[type="submit"]')
  await page.waitForURL(/\/customers/)
  await page.waitForSelector('table.table tbody tr')
  const rows = await page.locator('table.table tbody tr').count()
  log('Valid login lands on customers with 5 rows', rows === 5, `rows=${rows}`)

  await page.reload()
  await page.waitForSelector('table.table tbody tr')
  log('Session survives reload (cookie persisted)', !page.url().includes('/login'), page.url())
  completed = true
} finally {
  await browser.close()
  const failed = results.filter(r => !r.ok).length
  if (!completed) {
    console.log(`\nINCOMPLETE — aborted after ${results.length} of ${TOTAL_CHECKS} checks`)
  }
  console.log(`\nTOTAL: ${results.length}  PASSED: ${results.length - failed}  FAILED: ${failed}`)
  process.exitCode = (failed || !completed) ? 1 : 0
}
