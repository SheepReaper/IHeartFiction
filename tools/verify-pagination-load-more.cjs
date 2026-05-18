const https = require('node:https');
const { spawnSync } = require('node:child_process');

const baseUrl = process.env.BASE_URL ?? 'https://localhost:7000';
const allowInsecureTls = process.env.ALLOW_INSECURE_TLS === 'true';
const authorId = process.env.PAGINATION_AUTHOR_ID ?? '01KQWRX78K5X90V88QPBNNG77V';

const routes = [
  {
    name: 'stories',
    path: '/stories',
    loadMoreLabel: 'Load more stories',
    itemSelector: 'a[href*="/stories/"][href$="/read"]'
  },
  {
    name: 'authors',
    path: '/authors',
    loadMoreLabel: 'Load more authors',
    itemSelector: 'a[href^="/authors/"][class*="ihf-card-title-link"]'
  },
  {
    name: 'author-stories',
    path: `/authors/${authorId}/stories`,
    loadMoreLabel: 'Load more stories',
    itemSelector: 'a[href*="/stories/"][href$="/read"]'
  }
];

function getHtml(url) {
  return new Promise((resolve, reject) => {
    const req = https.request(url, {
      method: 'GET',
      rejectUnauthorized: !allowInsecureTls,
      headers: {
        'User-Agent': 'ihf-pagination-verifier/1.0'
      }
    }, (res) => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', chunk => body += chunk);
      res.on('end', () => resolve({ statusCode: res.statusCode ?? 0, body }));
    });

    req.on('error', reject);
    req.end();
  });
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function hasPaginationNav(html) {
  return html.includes('nav class="pagination is-centered is-sr-only"')
    || html.includes('nav class="pagination is-sr-only is-centered"');
}

function hasVisibleFallbackLink(html, label) {
  const pattern = new RegExp(`<a[^>]*>${escapeRegExp(label)}<\\/a>`, 'i');
  return pattern.test(html);
}

function hasHrefValue(html, href) {
  return html.includes(`href="${href}"`);
}

async function verifyPrerender(route, failures, pendingHydrationChecks) {
  const url = new URL(route.path, `${baseUrl}/`).toString();
  const response = await getHtml(url);

  if (response.statusCode < 200 || response.statusCode > 299) {
    failures.push(`${route.name}: request failed (${response.statusCode})`);
    return;
  }

  const hasLoadMore = hasVisibleFallbackLink(response.body, route.loadMoreLabel);
  const hasNextPage = hasVisibleFallbackLink(response.body, 'Next page');
  const hasPreviousPage = hasVisibleFallbackLink(response.body, 'Previous');

  if (hasLoadMore) {
    pendingHydrationChecks.push(route);
  }

  if (!hasLoadMore && !hasNextPage && !hasPreviousPage) {
    return;
  }

  if (!hasPaginationNav(response.body)) {
    failures.push(`${route.name}: pagination nav missing sr-only fallback markup`);
  }

  if (hasLoadMore && !hasHrefValue(response.body, 'rel="next"')) {
    failures.push(`${route.name}: load-more link missing rel=next`);
  }

  if (hasNextPage && !hasVisibleFallbackLink(response.body, 'Next page')) {
    failures.push(`${route.name}: next-page fallback link missing from prerendered HTML`);
  }
}

function psQuote(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

function runPlaywrightCli(command) {
  const result = spawnSync('pwsh', ['-NoProfile', '-Command', command], {
    encoding: 'utf8'
  });

  if (result.status !== 0) {
    const errorText = [result.stdout, result.stderr].filter(Boolean).join('\n').trim();
    throw new Error(errorText || `command failed: ${command}`);
  }

  return result.stdout;
}

function extractJsonResult(output) {
  const jsonLine = output.split(/\r?\n/).map(line => line.trim()).find(line => line.startsWith('{') && line.endsWith('}'));
  if (!jsonLine) {
    throw new Error(`no JSON result found in output: ${output}`);
  }

  return JSON.parse(jsonLine);
}

function closePlaywrightSessions() {
  try {
    runPlaywrightCli('playwright-cli close-all');
  }
  catch {
    // Ignore cleanup failures; they are non-fatal for verification output.
  }
}

async function verifyHydration(route, failures, results) {
  closePlaywrightSessions();

  const url = new URL(route.path, `${baseUrl}/`).toString();
  runPlaywrightCli(`playwright-cli open ${psQuote(url)}`);

  const stateCode = `async page => {
    await page.locator('main').waitFor({ state: 'visible', timeout: 30000 });
    const domState = await page.evaluate((loadMoreLabel) => {
      const links = Array.from(document.querySelectorAll('a'));
      const loadMoreLink = links.find(link => (link.textContent || '').trim() === loadMoreLabel);
      const fallbackNext = links.find(link => (link.textContent || '').trim() === 'Next page');

      return {
        loadMoreHref: loadMoreLink?.getAttribute('href') ?? null,
        fallbackNextHref: fallbackNext?.getAttribute('href') ?? null,
        hasLoadMore: Boolean(loadMoreLink)
      };
    }, ${JSON.stringify(route.loadMoreLabel)});

    return {
      url: page.url(),
      itemCount: await page.locator(${JSON.stringify(route.itemSelector)}).count(),
      loadMoreHref: domState.loadMoreHref,
      fallbackNextHref: domState.fallbackNextHref,
      hasLoadMore: domState.hasLoadMore
    };
  }`;

  const state = extractJsonResult(runPlaywrightCli(`playwright-cli run-code ${psQuote(stateCode)}`));

  if (!state.hasLoadMore || !state.loadMoreHref) {
    results.push(`${route.name}: skipped hydration append check (single-page dataset)`);
    closePlaywrightSessions();
    return;
  }

  const clickCode = `async page => {
    const beforeCount = await page.locator(${JSON.stringify(route.itemSelector)}).count();
    await page.evaluate((loadMoreLabel) => {
      const links = Array.from(document.querySelectorAll('a'));
      const loadMoreLink = links.find(link => (link.textContent || '').trim() === loadMoreLabel);
      loadMoreLink?.click();
    }, ${JSON.stringify(route.loadMoreLabel)});

    await page.waitForFunction(({ selector, previousCount }) => {
      return document.querySelectorAll(selector).length > previousCount;
    }, { selector: ${JSON.stringify(route.itemSelector)}, previousCount: beforeCount }, { timeout: 15000 });

    const remainingLoadMore = await page.evaluate((loadMoreLabel) => {
      const links = Array.from(document.querySelectorAll('a'));
      const loadMoreLink = links.find(link => (link.textContent || '').trim() === loadMoreLabel);
      return loadMoreLink?.getAttribute('href') ?? null;
    }, ${JSON.stringify(route.loadMoreLabel)});

    return {
      url: page.url(),
      itemCount: await page.locator(${JSON.stringify(route.itemSelector)}).count(),
      loadMoreHref: remainingLoadMore
    };
  }`;

  const afterState = extractJsonResult(runPlaywrightCli(`playwright-cli run-code ${psQuote(clickCode)}`));
  closePlaywrightSessions();

  if (afterState.url !== state.url) {
    failures.push(`${route.name}: hydration click changed URL from ${state.url} to ${afterState.url}`);
  }

  if (afterState.itemCount <= state.itemCount) {
    failures.push(`${route.name}: hydration click did not append items`);
  }

  results.push(`${route.name}: appended ${afterState.itemCount - state.itemCount} items in place`);
}

async function main() {
  const failures = [];
  const results = [];
  const pendingHydrationChecks = [];

  for (const route of routes) {
    try {
      await verifyPrerender(route, failures, pendingHydrationChecks);
    }
    catch (error) {
      failures.push(`${route.name}: prerender verification crashed (${error.message})`);
    }
  }

  for (const route of pendingHydrationChecks) {
    try {
      await verifyHydration(route, failures, results);
    }
    catch (error) {
      failures.push(`${route.name}: hydration verification failed (${error.message})`);
      closePlaywrightSessions();
    }
  }

  if (failures.length > 0) {
    console.error('Pagination verification failed:');
    for (const failure of failures) {
      console.error(` - ${failure}`);
    }
    process.exit(1);
  }

  for (const result of results) {
    console.log(result);
  }

  console.log(`Verified pagination fallback and hydration behavior on ${routes.length} routes at ${baseUrl}`);
}

main().catch(error => {
  console.error('Verification script crashed:', error);
  process.exit(1);
});