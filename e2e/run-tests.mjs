import { chromium } from 'playwright';

const BASE = 'http://localhost:54322';
const results = [];
function log(name, ok, detail = '') {
  results.push({ name, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  — ' + detail : ''}`);
}

// Scoped submit helper: clicks the submit button INSIDE the main content form,
// never the header's logout form.
async function submitMainForm(page, extraSelector = '') {
  const sel = `main form .actions button[type="submit"]${extraSelector}, main .card form button[type="submit"]${extraSelector}, main form button[type="submit"]${extraSelector}`;
  const loc = page.locator(sel).last();
  await Promise.all([
    page.waitForLoadState('networkidle'),
    loc.click()
  ]);
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext();
const page = await ctx.newPage();

try {
  // ---------- 1. Unauthenticated redirect ----------
  {
    const resp = await page.goto(BASE + '/', { waitUntil: 'domcontentloaded' });
    const url = page.url();
    log('Unauthenticated root redirects to login',
        url.includes('/Account/Login'),
        `landed on ${url} (status ${resp.status()})`);
  }

  // ---------- 2. Login page renders ----------
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const h1 = await page.locator('h1').first().innerText();
    log('Login page shows heading', /Tiny CRM/i.test(h1), `h1="${h1}"`);
    const hasUserField = await page.locator('input[name="Username"]').count();
    const hasPassField = await page.locator('input[name="Password"]').count();
    log('Login form has Username + Password fields', hasUserField === 1 && hasPassField === 1,
        `user=${hasUserField} pass=${hasPassField}`);
  }

  // ---------- 3. Login with wrong password ----------
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'wrongpass');
    await page.locator('.login-card button[type="submit"]').click();
    await page.waitForLoadState('networkidle');
    const text = await page.locator('.validation-summary, .field-error').first().innerText().catch(() => '');
    log('Wrong password shows validation error', /invalid/i.test(text), `text="${text.slice(0,80)}"`);
  }

  // ---------- 4. Login with correct credentials ----------
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await Promise.all([
      page.waitForLoadState('networkidle'),
      page.locator('.login-card button[type="submit"]').click()
    ]);
    const url = page.url();
    const h1 = await page.locator('h1').first().innerText().catch(() => '');
    log('Valid login lands on dashboard', /\/$|\/Dashboard/i.test(url) && /dashboard/i.test(h1),
        `url=${url} h1="${h1}"`);
  }

  // ---------- 5. Dashboard: stats + bars + recent ----------
  {
    const statCount = await page.locator('.stat').count();
    log('Dashboard has 3 stat cards', statCount === 3, `count=${statCount}`);
    const barRows = await page.locator('.bar-row').count();
    log('Dashboard has bar rows (status+type)', barRows >= 7, `barRows=${barRows}`);
    const recentH2 = await page.locator('h2', { hasText: 'Recent interactions' }).count();
    log('Dashboard has recent interactions section', recentH2 === 1);
    const newCustBtn = await page.locator('main a.btn-primary', { hasText: 'New customer' }).count();
    log('Dashboard has single New customer button', newCustBtn === 1, `count=${newCustBtn}`);
  }

  // ---------- 6. Customers index: list + no duplicate Details button ----------
  {
    await page.goto(BASE + '/Customers', { waitUntil: 'networkidle' });
    const rows = await page.locator('table.table tbody tr').count();
    log('Customers list has rows', rows >= 5, `rows=${rows}`);
    const detailsBtns = await page.locator('table.table tbody tr td.actions a:has-text("Details")').count();
    log('Customers list has NO duplicate Details button in actions', detailsBtns === 0,
        `detailsButtons=${detailsBtns}`);
    const editBtns = await page.locator('table.table tbody tr td.actions a:has-text("Edit")').count();
    const deleteBtns = await page.locator('table.table tbody tr td.actions a:has-text("Delete")').count();
    log('Each row has Edit + Delete', editBtns === rows && deleteBtns === rows,
        `edit=${editBtns} delete=${deleteBtns} rows=${rows}`);
    const nameLinks = await page.locator('table.table tbody tr td:first-child a').count();
    log('Customer name is a link to details', nameLinks === rows, `nameLinks=${nameLinks}`);
  }

  // ---------- 7. Customers: search filter ----------
  {
    await page.goto(BASE + '/Customers?search=Acme', { waitUntil: 'networkidle' });
    const rows = await page.locator('table.table tbody tr').count();
    const firstCell = await page.locator('table.table tbody tr td').first().innerText().catch(() => '');
    log('Search filter narrows results', rows >= 1 && /acme/i.test(firstCell),
        `rows=${rows} first="${firstCell}"`);
  }

  // ---------- 8. Customers: status filter ----------
  {
    await page.goto(BASE + '/Customers?status=Lead', { waitUntil: 'networkidle' });
    const rows = await page.locator('table.table tbody tr').count();
    const badges = await page.locator('table.table tbody tr .badge-lead').count();
    log('Status filter shows only Lead customers', rows === badges && rows >= 1,
        `rows=${rows} leadBadges=${badges}`);
  }

  // ---------- 9. Create customer (valid) ----------
  {
    await page.goto(BASE + '/Customers/Create', { waitUntil: 'networkidle' });
    const h2 = await page.locator('h2').first().innerText();
    log('Create page heading', /new customer/i.test(h2), `h2="${h2}"`);
    const custName = 'PW Corp ' + Date.now();
    await page.fill('input[name="Name"]', custName);
    await page.fill('input[name="Company"]', 'PW Inc');
    await page.fill('input[name="Email"]', 'pw@test.com');
    await page.fill('input[name="Phone"]', '+1 555 000 1111');
    await page.selectOption('select[name="Status"]', 'Customer');
    await page.fill('textarea[name="Notes"]', 'Created by e2e test');
    await submitMainForm(page);
    const url = page.url();
    const success = await page.locator('.alert-success', { hasText: 'Customer added' }).count();
    log('Create valid customer redirects to list with success', /\/Customers$/.test(url) && success === 1,
        `url=${url} success=${success}`);
    const inList = await page.locator('table.table tbody tr td:has-text("' + custName + '")').count();
    log('Created customer appears in list', inList === 1, `inList=${inList} name=${custName}`);
  }

  // ---------- 10. Create customer (invalid: short name + bad email) ----------
  {
    await page.goto(BASE + '/Customers/Create', { waitUntil: 'networkidle' });
    await page.fill('input[name="Name"]', 'X');
    await page.fill('input[name="Email"]', 'notanemail');
    await submitMainForm(page);
    const errors = await page.locator('.field-error').count();
    log('Invalid create shows validation errors', errors >= 2, `errors=${errors}`);
    const stayed = page.url();
    log('Invalid create stays on Create page', /\/Customers\/Create/.test(stayed), `url=${stayed}`);
  }

  // ---------- 11. Customer details ----------
  {
    await page.goto(BASE + '/Customers/Details/1', { waitUntil: 'networkidle' });
    const h1 = await page.locator('h1').first().innerText();
    log('Details page shows customer name as h1', h1.length > 0, `h1="${h1}"`);
    const interH2 = await page.locator('h2', { hasText: 'Interactions' }).count();
    log('Details page has Interactions section', interH2 === 1);
    const logBtn = await page.locator('main a.btn-primary', { hasText: 'Log interaction' }).count();
    log('Details page has Log interaction button', logBtn === 1, `count=${logBtn}`);
    const interRows = await page.locator('table.table tbody tr').count();
    log('Details page lists interactions', interRows >= 1, `rows=${interRows}`);
  }

  // ---------- 12. Edit customer ----------
  {
    await page.goto(BASE + '/Customers/Edit/1', { waitUntil: 'networkidle' });
    const h2 = await page.locator('h2').first().innerText();
    log('Edit page heading', /edit customer/i.test(h2), `h2="${h2}"`);
    const nameVal = await page.locator('input[name="Name"]').inputValue();
    await page.fill('input[name="Name"]', nameVal + ' Edited');
    await submitMainForm(page);
    const success = await page.locator('.alert-success', { hasText: 'Customer updated' }).count();
    log('Edit submits and shows success', success === 1, `success=${success}`);
    // revert
    await page.goto(BASE + '/Customers/Edit/1', { waitUntil: 'networkidle' });
    const cur = await page.locator('input[name="Name"]').inputValue();
    await page.fill('input[name="Name"]', cur.replace(' Edited', ''));
    await submitMainForm(page);
  }

  // ---------- 13. Log interaction (valid) ----------
  {
    await page.goto(BASE + '/Customers/Details/1', { waitUntil: 'networkidle' });
    await page.click('main a.btn-primary:has-text("Log interaction")');
    await page.waitForLoadState('networkidle');
    const url = page.url();
    log('Log interaction link goes to Interactions/Create', /Interactions\/Create/.test(url), `url=${url}`);
    const heading = await page.locator('h2').first().innerText();
    log('Log interaction shows customer name in heading', /For .+/.test(heading), `h2="${heading}"`);
    await page.selectOption('select[name="Type"]', 'Email');
    const today = new Date().toISOString().slice(0,10);
    await page.fill('input[name="InteractionDate"]', today);
    await page.fill('input[name="Subject"]', 'E2E test interaction');
    await page.fill('textarea[name="Notes"]', 'Logged by playwright');
    await submitMainForm(page);
    const back = page.url();
    const inList = await page.locator('table.table tbody tr td:has-text("E2E test interaction")').count();
    log('Log interaction redirects back to details', /\/Customers\/Details\/1/.test(back), `url=${back}`);
    log('Logged interaction appears in list', inList === 1, `inList=${inList}`);
  }

  // ---------- 14. Log interaction (invalid: future date) ----------
  {
    await page.goto(BASE + '/Interactions/Create?customerId=1', { waitUntil: 'networkidle' });
    await page.selectOption('select[name="Type"]', 'Call');
    const future = new Date(Date.now() + 86400000*10).toISOString().slice(0,10);
    await page.fill('input[name="InteractionDate"]', future);
    await page.fill('input[name="Subject"]', 'Future test');
    await submitMainForm(page);
    const errors = await page.locator('.field-error, .field-validation-error, .validation-summary-errors, .validation-summary').count();
    const futureMsg = await page.locator('span:has-text("in the future")').count();
    log('Future interaction date rejected', errors >= 1 || futureMsg >= 1, `errors=${errors} futureMsg=${futureMsg}`);
  }

  // ---------- 15. Delete interaction ----------
  {
    await page.goto(BASE + '/Customers/Details/1', { waitUntil: 'networkidle' });
    const before = await page.locator('table.table tbody tr').count();
    const firstDelete = page.locator('table.table tbody tr').first().locator('button:has-text("Delete")');
    await Promise.all([
      page.waitForLoadState('networkidle'),
      firstDelete.click()
    ]);
    const after = await page.locator('table.table tbody tr').count();
    log('Delete interaction removes a row', after === before - 1, `before=${before} after=${after}`);
  }

  // ---------- 16. Delete customer (cancel) ----------
  {
    // create a throwaway customer for delete tests so they're independent of prior runs
    await page.goto(BASE + '/Customers/Create', { waitUntil: 'networkidle' });
    const tmpName = 'DeleteMe ' + Date.now();
    await page.fill('input[name="Name"]', tmpName);
    await page.fill('input[name="Company"]', 'Tmp');
    await page.fill('input[name="Email"]', 'd@e.com');
    await page.selectOption('select[name="Status"]', 'Lead');
    await submitMainForm(page);
    await page.waitForLoadState('networkidle');
    // find its details link
    const detailLink = page.locator('table.table tbody tr td a:has-text("' + tmpName + '")').first();
    const href = await detailLink.getAttribute('href');
    const idMatch = href && href.match(/\/Customers\/Details\/(\d+)/);
    const delId = idMatch ? idMatch[1] : null;
    log('Setup: created throwaway customer for delete tests', delId !== null, `id=${delId} name=${tmpName}`);

    await page.goto(BASE + '/Customers/Delete/' + delId, { waitUntil: 'networkidle' });
    const h2 = await page.locator('h2').first().innerText();
    log('Delete confirm page heading', /delete customer/i.test(h2), `h2="${h2}"`);
    await page.click('main a.btn-secondary:has-text("Cancel")');
    await page.waitForLoadState('networkidle');
    const stillExists = await page.locator('table.table tbody tr td a:has-text("' + tmpName + '")').count();
    log('Cancel delete keeps the customer', stillExists >= 1, `stillExists=${stillExists}`);

    // ---------- 17. Delete customer (confirm) ----------
    await page.goto(BASE + '/Customers/Delete/' + delId, { waitUntil: 'networkidle' });
    await Promise.all([
      page.waitForLoadState('networkidle'),
      page.locator('main button.btn-danger:has-text("Delete")').click()
    ]);
    const success = await page.locator('.alert-success', { hasText: 'Customer deleted' }).count();
    log('Delete confirmed shows success', success === 1, `success=${success}`);
    const gone = await page.locator('table.table tbody tr td a:has-text("' + tmpName + '")').count();
    log('Deleted customer gone from list', gone === 0, `gone=${gone}`);
  }

  // ---------- 18. Reports page ----------
  {
    await page.goto(BASE + '/Reports', { waitUntil: 'networkidle' });
    const h1 = await page.locator('h1').first().innerText();
    log('Reports page heading', /reports/i.test(h1), `h1="${h1}"`);
    const cards = await page.locator('main .card').count();
    log('Reports has 3 cards (status/types/customers)', cards >= 3, `cards=${cards}`);
    const exportBtn = await page.locator('main a.btn-primary:has-text("Export customers (CSV)")').count();
    log('Reports has CSV export button', exportBtn === 1, `count=${exportBtn}`);
    const newCustOnReports = await page.locator('main a:has-text("New customer")').count();
    log('Reports has NO duplicate New customer button', newCustOnReports === 0, `count=${newCustOnReports}`);
  }

  // ---------- 19. CSV export ----------
  {
    await page.goto(BASE + '/Reports', { waitUntil: 'networkidle' });
    const [download] = await Promise.all([
      ctx.waitForEvent('download'),
      page.locator('main a.btn-primary:has-text("Export customers (CSV)")').click()
    ]);
    const fname = download.suggestedFilename();
    const stream = await download.createReadStream();
    const chunks = [];
    for await (const c of stream) chunks.push(c);
    const text = Buffer.concat(chunks).toString('utf8');
    const firstLine = text.split('\n')[0];
    log('CSV downloads with correct headers', /Id,Name,Company,Email,Phone,Status,InteractionCount/.test(firstLine),
        `filename="${fname}" firstLine="${firstLine.slice(0,60)}"`);
    log('CSV has data rows', text.split('\n').length >= 3, `lines=${text.split('\n').length}`);
  }

  // ---------- 20. Logout ----------
  {
    await page.goto(BASE + '/', { waitUntil: 'domcontentloaded' });
    await Promise.all([
      page.waitForLoadState('domcontentloaded'),
      page.locator('header button:has-text("Sign out")').click()
    ]);
    const url = page.url();
    log('Logout returns to login page', /Account\/Login/.test(url), `url=${url}`);
  }

  // ---------- 21. Auth protects routes after logout ----------
  {
    await page.goto(BASE + '/Customers', { waitUntil: 'domcontentloaded' });
    const url = page.url();
    log('Protected route redirects to login after logout', url.includes('/Account/Login'), `url=${url}`);
  }

} catch (e) {
  console.error('TEST RUN ERROR:', e.message);
  process.exitCode = 2;
} finally {
  await browser.close();
  const passed = results.filter(r => r.ok).length;
  const failed = results.filter(r => !r.ok).length;
  console.log('\n========================================');
  console.log(`TOTAL: ${results.length}   PASSED: ${passed}   FAILED: ${failed}`);
  console.log('========================================');
  if (failed > 0) {
    console.log('\nFailed tests:');
    results.filter(r => !r.ok).forEach(r => console.log(`  - ${r.name}  (${r.detail})`));
    process.exitCode = 1;
  } else {
    process.exitCode = 0;
  }
}