const path = require('node:path');
const { chromium } = require(path.resolve(__dirname, '../.artifacts/browser-validation/node_modules/playwright'));

(async () => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
  });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 960 },
    ignoreHTTPSErrors: true
  });
  const page = await context.newPage();

  console.log('Navigating to http://localhost:14134/ ...');
  await page.goto('http://localhost:14134/', { waitUntil: 'networkidle' });

  // 1. Light Mode
  await page.evaluate(() => {
    document.documentElement.setAttribute('data-theme', 'light');
  });
  await page.waitForTimeout(500);

  const outDir = 'C:\\Users\\bgonz\\.gemini\\antigravity\\brain\\8a3282f0-3ae8-4a2c-a750-16300c03590c';
  const lightPath = path.join(outDir, 'homepage-light.png');
  await page.screenshot({ path: lightPath, fullPage: true });
  console.log('Saved light mode screenshot to:', lightPath);

  // Inspect light styles
  const lightStyles = await page.evaluate(() => {
    const hero = document.querySelector('.hero.is-primary');
    const heroBtn = document.querySelector('.hero .button.is-primary.is-inverted');
    const authorCard = document.querySelector('.box.has-background-primary-soft');
    const tag = document.querySelector('.tag.is-info');
    return {
      heroBg: hero ? getComputedStyle(hero).backgroundColor : null,
      heroBtnBg: heroBtn ? getComputedStyle(heroBtn).backgroundColor : null,
      heroBtnColor: heroBtn ? getComputedStyle(heroBtn).color : null,
      authorCardBg: authorCard ? getComputedStyle(authorCard).backgroundColor : null,
      authorCardColor: authorCard ? getComputedStyle(authorCard).color : null,
      tagBg: tag ? getComputedStyle(tag).backgroundColor : null,
      tagColor: tag ? getComputedStyle(tag).color : null,
    };
  });
  console.log('Light Mode Styles:', lightStyles);

  // 2. Dark Mode
  await page.evaluate(() => {
    document.documentElement.setAttribute('data-theme', 'dark');
  });
  await page.waitForTimeout(500);

  const darkPath = path.join(outDir, 'homepage-dark.png');
  await page.screenshot({ path: darkPath, fullPage: true });
  console.log('Saved dark mode screenshot to:', darkPath);

  // Inspect dark styles
  const darkStyles = await page.evaluate(() => {
    const hero = document.querySelector('.hero.is-primary');
    const heroBtn = document.querySelector('.hero .button.is-primary.is-inverted');
    const authorCard = document.querySelector('.box.has-background-primary-soft');
    const tag = document.querySelector('.tag.is-info');
    const card = document.querySelector('.card');
    return {
      heroBg: hero ? getComputedStyle(hero).backgroundColor : null,
      heroBtnBg: heroBtn ? getComputedStyle(heroBtn).backgroundColor : null,
      heroBtnColor: heroBtn ? getComputedStyle(heroBtn).color : null,
      authorCardBg: authorCard ? getComputedStyle(authorCard).backgroundColor : null,
      authorCardColor: authorCard ? getComputedStyle(authorCard).color : null,
      tagBg: tag ? getComputedStyle(tag).backgroundColor : null,
      tagColor: tag ? getComputedStyle(tag).color : null,
      cardBg: card ? getComputedStyle(card).backgroundColor : null,
      cardColor: card ? getComputedStyle(card).color : null,
    };
  });
  console.log('Dark Mode Styles:', darkStyles);

  await browser.close();
  console.log('Playwright run completed successfully.');
})();
