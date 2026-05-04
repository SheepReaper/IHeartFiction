const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const baseUrl = 'https://localhost:7000';
const outputPath = path.join(process.cwd(), 'tools', 'playwright-ui-audit-results.json');
const routes = [
  '/',
  '/stories',
  '/authors',
  '/authors/01K52M35BFK3ED1HB7V7FY353C',
  '/authors/01K52M35BFK3ED1HB7V7FY353C/stories',
  '/stories/01K6S20WDTEWH7Q3MRJM0G9MRH',
  '/stories/01K6S20WDTEWH7Q3MRJM0G9MRH/read',
  '/stories/01K6SFB0G8EQ5MVNZ0RC6V1QCZ/chapters/01K7J887GFPBQEJKE0Z6ACMP1Z',
  '/about'
];
const themes = ['light', 'dark'];

function routeUrl(route, theme) {
  return `${baseUrl}${route}${route.includes('?') ? '&' : '?'}theme=${theme}`;
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1600, height: 1200 },
    ignoreHTTPSErrors: true
  });
  const page = await context.newPage();
  const results = [];

  for (const route of routes) {
    for (const theme of themes) {
      const url = routeUrl(route, theme);
      const consoleErrors = [];
      const failedRequests = [];
      const response = await page.goto(url, { waitUntil: 'networkidle', timeout: 60000 });

      page.removeAllListeners('console');
      page.removeAllListeners('requestfailed');
      page.on('console', message => {
        if (message.type() === 'error') {
          consoleErrors.push(message.text());
        }
      });
      page.on('requestfailed', request => {
        failedRequests.push({ url: request.url(), failure: request.failure()?.errorText ?? 'unknown' });
      });

      await page.waitForTimeout(200);

      const pageAudit = await page.evaluate(() => {
        function parseColor(value) {
          if (!value) return null;
          const match = value.match(/rgba?\(([^)]+)\)/i);
          if (!match) return null;
          const parts = match[1].split(',').map(part => Number.parseFloat(part.trim()));
          if (parts.length < 3 || parts.some(Number.isNaN)) return null;
          return { r: parts[0], g: parts[1], b: parts[2], a: parts[3] ?? 1 };
        }

        function composite(foreground, background) {
          const alpha = foreground.a ?? 1;
          if (alpha >= 1) {
            return { r: foreground.r, g: foreground.g, b: foreground.b, a: 1 };
          }

          return {
            r: foreground.r * alpha + background.r * (1 - alpha),
            g: foreground.g * alpha + background.g * (1 - alpha),
            b: foreground.b * alpha + background.b * (1 - alpha),
            a: 1
          };
        }

        function channel(value) {
          const normalized = value / 255;
          return normalized <= 0.03928 ? normalized / 12.92 : Math.pow((normalized + 0.055) / 1.055, 2.4);
        }

        function luminance(color) {
          return 0.2126 * channel(color.r) + 0.7152 * channel(color.g) + 0.0722 * channel(color.b);
        }

        function contrastRatio(foreground, background) {
          const lighter = Math.max(luminance(foreground), luminance(background));
          const darker = Math.min(luminance(foreground), luminance(background));
          return (lighter + 0.05) / (darker + 0.05);
        }

        function effectiveBackground(element) {
          let current = element;
          let background = parseColor(getComputedStyle(document.body).backgroundColor) ?? { r: 255, g: 255, b: 255, a: 1 };

          while (current) {
            const parsed = parseColor(getComputedStyle(current).backgroundColor);
            if (parsed && parsed.a > 0) {
              background = composite(parsed, background);
              if ((parsed.a ?? 1) >= 1) {
                return background;
              }
            }
            current = current.parentElement;
          }

          return background;
        }

        function isVisible(element) {
          const style = getComputedStyle(element);
          const rect = element.getBoundingClientRect();
          return style.visibility !== 'hidden'
            && style.display !== 'none'
            && Number.parseFloat(style.opacity || '1') > 0
            && rect.width > 0
            && rect.height > 0;
        }

        function textContent(element) {
          return (element.textContent || '').replace(/\s+/g, ' ').trim();
        }

        function isLeafText(element) {
          if (!textContent(element)) return false;
          for (const child of element.children) {
            if (textContent(child)) {
              return false;
            }
          }
          return true;
        }

        function isLargeText(style) {
          const fontSize = Number.parseFloat(style.fontSize || '16');
          const fontWeight = Number.parseInt(style.fontWeight || '400', 10);
          return fontSize >= 24 || (fontSize >= 18.66 && fontWeight >= 700);
        }

        const selectors = [
          'main h1',
          'main h2',
          'main h3',
          'main h4',
          'main h5',
          'main h6',
          'main p',
          'main a',
          'main button',
          'main li',
          'nav .navbar-item',
          'nav .button',
          '.card .title',
          '.card .title a',
          '.card .subtitle',
          '.card .content > p',
          '.card .content em',
          '.card .card-footer-item',
          '.message-body',
          '.notification',
          '.box',
          '.level-right .is-size-7'
        ];

        const seen = new Set();
        const contrastFailures = [];

        for (const selector of selectors) {
          for (const element of document.querySelectorAll(selector)) {
            if (seen.has(element) || !isVisible(element) || !isLeafText(element)) {
              continue;
            }
            seen.add(element);

            const style = getComputedStyle(element);
            const foreground = parseColor(style.color);
            if (!foreground) {
              continue;
            }

            const background = effectiveBackground(element);
            const effectiveForeground = composite(foreground, background);
            const ratio = contrastRatio(effectiveForeground, background);
            const threshold = isLargeText(style) ? 3 : 4.5;

            if (ratio < threshold) {
              contrastFailures.push({
                kind: 'text',
                selector,
                text: textContent(element).slice(0, 120),
                ratio: Number(ratio.toFixed(2)),
                threshold,
                color: style.color,
                background: `rgb(${Math.round(background.r)}, ${Math.round(background.g)}, ${Math.round(background.b)})`
              });
            }
          }
        }

        return {
          title: document.title,
          dataTheme: document.documentElement.getAttribute('data-theme'),
          contrastFailures
        };
      });

      const tooltipFailures = [];
      const tooltipElements = await page.locator('[data-tooltip]').elementHandles();
      for (const element of tooltipElements) {
        await element.hover();
        await page.waitForTimeout(120);

        const tooltipAudit = await element.evaluate(node => {
          function parseColor(value) {
            if (!value) return null;
            const match = value.match(/rgba?\(([^)]+)\)/i);
            if (!match) return null;
            const parts = match[1].split(',').map(part => Number.parseFloat(part.trim()));
            if (parts.length < 3 || parts.some(Number.isNaN)) return null;
            return { r: parts[0], g: parts[1], b: parts[2], a: parts[3] ?? 1 };
          }

          function channel(value) {
            const normalized = value / 255;
            return normalized <= 0.03928 ? normalized / 12.92 : Math.pow((normalized + 0.055) / 1.055, 2.4);
          }

          function luminance(color) {
            return 0.2126 * channel(color.r) + 0.7152 * channel(color.g) + 0.0722 * channel(color.b);
          }

          function contrastRatio(foreground, background) {
            const lighter = Math.max(luminance(foreground), luminance(background));
            const darker = Math.min(luminance(foreground), luminance(background));
            return (lighter + 0.05) / (darker + 0.05);
          }

          const style = getComputedStyle(node, '::after');
          const content = style.content?.replace(/^"|"$/g, '');
          if (!content || content === 'none' || Number.parseFloat(style.opacity || '0') === 0) {
            return null;
          }

          const foreground = parseColor(style.color);
          const background = parseColor(style.backgroundColor);
          if (!foreground || !background) {
            return null;
          }

          return {
            text: content,
            ratio: Number(contrastRatio(foreground, background).toFixed(2)),
            color: style.color,
            background: style.backgroundColor,
            title: node.getAttribute('title'),
            tooltip: node.getAttribute('data-tooltip')
          };
        });

        if (tooltipAudit && tooltipAudit.ratio < 4.5) {
          tooltipFailures.push({ kind: 'tooltip', ...tooltipAudit });
        }
      }

      results.push({
        route,
        theme,
        url,
        status: response?.status() ?? null,
        title: pageAudit.title,
        dataTheme: pageAudit.dataTheme,
        consoleErrors,
        failedRequests,
        contrastFailures: pageAudit.contrastFailures,
        tooltipFailures
      });
    }
  }

  await context.close();
  await browser.close();

  const summary = {
    totalChecks: results.length,
    badStatus: results.filter(result => result.status !== 200),
    badTheme: results.filter(result => result.dataTheme !== result.theme),
    consoleNoise: results.filter(result => result.consoleErrors.length > 0),
    networkIssues: results.filter(result => result.failedRequests.length > 0),
    contrastFailures: results.filter(result => result.contrastFailures.length > 0),
    tooltipFailures: results.filter(result => result.tooltipFailures.length > 0),
    results
  };

  fs.writeFileSync(outputPath, JSON.stringify(summary, null, 2));
  console.log(JSON.stringify({
    totalChecks: summary.totalChecks,
    badStatus: summary.badStatus.length,
    badTheme: summary.badTheme.length,
    consoleNoise: summary.consoleNoise.length,
    networkIssues: summary.networkIssues.length,
    contrastFailures: summary.contrastFailures.length,
    tooltipFailures: summary.tooltipFailures.length,
    outputPath
  }, null, 2));
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
