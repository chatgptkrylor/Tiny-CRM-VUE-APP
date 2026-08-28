import { chromium } from 'playwright'

const BASE = 'http://localhost:5173'
const results = []
const log = (name, ok, detail = '') => {
  results.push({ name, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  - ' + detail : ''}`)
}

const browser = await chromium.launch({ headless: true })
const page = await browser.newPage()

try {
  await page.goto(BASE + '/customers', { waitUntil: 'domcontentloaded' })
  await page.waitForURL(/\/login/)
  log('Unauthenticated route redirects to login', /returnUrl/.test(page.url()), page.url())

  await page.fill('input[name="Username"]', 'admin')
  await page.fill('input[name="Password"]', 'wrongpass')
  await page.click('button[type="submit"]')
  await page.waitForSelector('.validation-summary')
  log('Wrong password shows error', true)

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
} finally {
  await browser.close()
  const failed = results.filter(r => !r.ok).length
  console.log(`\nTOTAL: ${results.length}  PASSED: ${results.length - failed}  FAILED: ${failed}`)
  process.exitCode = failed ? 1 : 0
}
