// Requires Playwright and Chromium. Run against a running development app:
// node tools/playwright-reader-resume.cjs --story-id <story-with-two-chapters>
// Optional: --base-url https://localhost:7000 --api-base-url https://localhost:7122
// Add --reject-first-observation to exercise recovery after a detached-body race.
// Uses isolated browser storage and records qualified reads on the target.
const assert = require('node:assert/strict');
const { parseArgs } = require('node:util');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const { values } = parseArgs({ options: {
  'story-id': { type: 'string' },
  'reject-first-observation': { type: 'boolean', default: false },
  'base-url': { type: 'string', default: 'https://localhost:7000' },
  'api-base-url': { type: 'string', default: 'https://localhost:7122' }
} });
assert.ok(values['story-id'], '--story-id is required');
const storyId = values['story-id'];
const baseUrl = values['base-url'].replace(/\/$/, '');
const apiBaseUrl = values['api-base-url'].replace(/\/$/, '');

(async () => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined
  });
  try {
    const context = await browser.newContext({ ignoreHTTPSErrors: true, serviceWorkers: 'block' });
    const response = await context.request.get(`${apiBaseUrl}/works/${storyId}/meta`);
    assert.equal(response.status(), 200, 'Published story metadata must be available');
    const meta = await response.json();
    assert.equal(meta.isDirectlyReadable, false, 'Use a container story');
    const first = meta.defaultReadableWorkId;
    const later = meta.readableChildren.find(child => child.id !== first)?.id;
    assert.ok(first && later, 'The story must have at least two published readable chapters');
    const progressKey = `reader:lastRead:${meta.id}`;
    const storyUrl = `${baseUrl}/stories/${meta.id}`;
    const page = await context.newPage();
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));

    if (values['reject-first-observation']) {
      // Reproduce an observation request arriving before its body is attached.
      await page.route('**/StoryReader.*.razor.js', async route => {
        const response = await route.fetch();
        const original = await response.text();
        const signature = 'export function observe(element, dotNetReference, workId) {';
        assert.ok(original.includes(signature), 'The reader observation module must be intercepted');
        const body = original.replace(signature, `${signature}
          window.readerObservationAttempts = (window.readerObservationAttempts || 0) + 1;
          if (!window.readerRejectedObservation) {
            window.readerRejectedObservation = true;
            element = null;
          }`);
        await route.fulfill({ response, body });
      });
    }

    // Observe the real protected-storage write without decoding its value.
    await page.addInitScript(() => {
      window.readerProgressWrites = [];
      const originalSetItem = Storage.prototype.setItem;
      Storage.prototype.setItem = function (key, value) {
        const result = originalSetItem.call(this, key, value);
        if (key.startsWith('reader:lastRead:')) window.readerProgressWrites.push(key);
        return result;
      };
    });

    async function readChapter(chapterId) {
      await page.goto(`${baseUrl}/read/${chapterId}`, { waitUntil: 'domcontentloaded' });
      await page.bringToFront();
      if (values['reject-first-observation']) {
        await page.waitForFunction(() => window.readerRejectedObservation === true);
        await page.getByRole('button', { name: 'Hit the lights', exact: true }).click();
        await page.waitForFunction(() => window.readerObservationAttempts > 1, null,
          { timeout: 5000 });
      }
      // Repeat interaction while waiting so a click before hydration isn't lost.
      // Qualification still requires the reader's real ten-second active timer.
      for (let attempt = 0; attempt < 30; attempt++) {
        await page.locator('.ihf-reader-body').click();
        if (await page.evaluate(key => window.readerProgressWrites.includes(key), progressKey)) break;
        await page.waitForTimeout(1000);
      }
      assert.ok(await page.evaluate(key => window.readerProgressWrites.includes(key), progressKey),
        'The current chapter must produce a fresh qualified-progress write');
      assert.ok(await page.evaluate(key => localStorage.getItem(key), progressKey),
        'A qualifying read must persist protected progress');
    }

    async function openStory() {
      await page.goto(storyUrl, { waitUntil: 'domcontentloaded' });
      await page.locator('a[href^="/read/"]').first().waitFor();
    }

    // No progress: the initial response already includes a direct chapter link.
    const initial = await page.goto(storyUrl, { waitUntil: 'domcontentloaded' });
    const html = await initial.text();
    assert.ok(html.includes(`href="/read/${first}"`));
    assert.ok(html.includes('Start Reading'));

    await readChapter(later);
    await openStory();
    const resume = page.getByRole('link', { name: 'Resume Reading', exact: true });
    await resume.waitFor(); // Exercises actual hydration and protected storage.
    assert.equal(await resume.getAttribute('href'), `/read/${later}`);
    assert.equal(await page.getByRole('link', { name: 'Start from beginning', exact: true }).getAttribute('href'),
      `/read/${first}`);
    assert.equal(page.url(), storyUrl, 'Story details must not automatically navigate');
    await resume.click();
    await page.waitForURL(`${baseUrl}/read/${later}`);

    // Explicit chapter navigation must not be overridden by saved progress.
    // Saving the default chapter returns the button to Start Reading.
    await readChapter(first);
    await openStory();
    const start = page.getByRole('link', { name: 'Start Reading', exact: true });
    await start.waitFor();
    assert.equal(await start.getAttribute('href'), `/read/${first}`);
    await start.click();
    await page.waitForURL(`${baseUrl}/read/${first}`);
    assert.deepEqual(pageErrors, [], 'The workflow must not produce page errors');
    console.log('PASS: SSR link, progress persistence, hydrated resume, direct navigation, and default-chapter behavior.');
    await context.close();
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
