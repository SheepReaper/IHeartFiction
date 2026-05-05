const https = require('node:https');

const baseUrl = process.env.BASE_URL ?? 'https://localhost:7000';
const authorId = process.env.SSP_AUTHOR_ID ?? '01K52M35BFK3ED1HB7V7FY353C';
const storyId = process.env.SSP_STORY_ID ?? '01K6S20WDTEWH7Q3MRJM0G9MRH';
const chapterStoryId = process.env.SSP_CHAPTER_STORY_ID ?? '01K6SFB0G8EQ5MVNZ0RC6V1QCZ';
const chapterId = process.env.SSP_CHAPTER_ID ?? '01K7J887GFPBQEJKE0Z6ACMP1Z';
const allowInsecureTls = process.env.ALLOW_INSECURE_TLS === 'true';

const routes = [
  '/',
  '/about',
  '/authors',
  '/stories',
  `/authors/${authorId}`,
  `/stories/${storyId}`,
  `/stories/${chapterStoryId}/chapters/${chapterId}`,
  `/stories/${storyId}/read`,
  `/authors/${authorId}/stories`
];

const requiredSnippets = [
  '<meta name="description"',
  '<link rel="canonical"',
  '<meta property="og:title"',
  '<meta property="og:description"',
  '<meta property="og:url"',
  '<meta property="og:type"',
  '<meta property="og:site_name"',
  '<meta property="og:image"',
  '<meta name="twitter:card"',
  '<meta name="twitter:title"',
  '<meta name="twitter:description"',
  '<meta name="twitter:image"'
];

function getHtml(url) {
  return new Promise((resolve, reject) => {
    const req = https.request(url, {
      method: 'GET',
      rejectUnauthorized: !allowInsecureTls,
      headers: {
        'User-Agent': 'ihf-social-metadata-verifier/1.0'
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

function findMissingSnippets(html) {
  return requiredSnippets.filter(snippet => !html.includes(snippet));
}

function titleFromHtml(html) {
  const match = html.match(/<title>([\s\S]*?)<\/title>/i);
  return match?.[1]?.trim() ?? '';
}

function extractMetaContent(html, property) {
  const escapedProperty = property.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const propertyFirst = new RegExp(`<meta\\s+[^>]*property=["']${escapedProperty}["'][^>]*content=["']([^"']*)["'][^>]*>`, 'i');
  const contentFirst = new RegExp(`<meta\\s+[^>]*content=["']([^"']*)["'][^>]*property=["']${escapedProperty}["'][^>]*>`, 'i');
  return propertyFirst.exec(html)?.[1] ?? contentFirst.exec(html)?.[1] ?? '';
}

function extractMetaNameContent(html, name) {
  const escapedName = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const nameFirst = new RegExp(`<meta\\s+[^>]*name=["']${escapedName}["'][^>]*content=["']([^"']*)["'][^>]*>`, 'i');
  const contentFirst = new RegExp(`<meta\\s+[^>]*content=["']([^"']*)["'][^>]*name=["']${escapedName}["'][^>]*>`, 'i');
  return nameFirst.exec(html)?.[1] ?? contentFirst.exec(html)?.[1] ?? '';
}

function checkTitleLength(html) {
  const ogTitle = extractMetaContent(html, 'og:title');
  if (!ogTitle) {
    return 'missing og:title content';
  }

  if (ogTitle.length > 60) {
    return `og:title too long (${ogTitle.length} > 60)`;
  }

  return null;
}

function checkDescriptionLength(html) {
  const twitterDescription = extractMetaNameContent(html, 'twitter:description');
  if (!twitterDescription) {
    return 'missing twitter:description content';
  }

  if (twitterDescription.length > 200) {
    return `twitter:description too long (${twitterDescription.length} > 200)`;
  }

  return null;
}

function checkImageDimensions(html) {
  const width = Number.parseInt(extractMetaContent(html, 'og:image:width'), 10);
  const height = Number.parseInt(extractMetaContent(html, 'og:image:height'), 10);

  if (Number.isNaN(width) || Number.isNaN(height)) {
    return 'missing og:image:width or og:image:height content';
  }

  if (width !== 1200 || height !== 630) {
    return `og:image dimensions are ${width}x${height} but expected 1200x630`;
  }

  return null;
}

async function main() {
  const failures = [];

  for (const route of routes) {
    const url = new URL(route, `${baseUrl}/`).toString();

    let response;
    try {
      response = await getHtml(url);
    } catch (error) {
      failures.push(`${route}: request error (${error.message})`);
      continue;
    }

    if (response.statusCode < 200 || response.statusCode > 299) {
      failures.push(`${route}: request failed (${response.statusCode})`);
      continue;
    }

    const missing = findMissingSnippets(response.body);
    if (missing.length > 0) {
      failures.push(`${route}: missing ${missing.join(', ')}`);
    }

    const title = titleFromHtml(response.body);
    if (!title || /Story Details - IHeartFiction|Reading Chapter - IHeartFiction|Author Profile - IHeartFiction/i.test(title)) {
      failures.push(`${route}: title is still generic ('${title || '(empty)'}')`);
    }

    const titleLengthIssue = checkTitleLength(response.body);
    if (titleLengthIssue) {
      failures.push(`${route}: ${titleLengthIssue}`);
    }

    const descriptionLengthIssue = checkDescriptionLength(response.body);
    if (descriptionLengthIssue) {
      failures.push(`${route}: ${descriptionLengthIssue}`);
    }

    const imageDimensionsIssue = checkImageDimensions(response.body);
    if (imageDimensionsIssue) {
      failures.push(`${route}: ${imageDimensionsIssue}`);
    }
  }

  if (failures.length > 0) {
    console.error('Social metadata verification failed:');
    for (const failure of failures) {
      console.error(` - ${failure}`);
    }
    process.exit(1);
  }

  console.log(`Verified social metadata on ${routes.length} routes at ${baseUrl}`);
}

main().catch(error => {
  console.error('Verification script crashed:', error);
  process.exit(1);
});
