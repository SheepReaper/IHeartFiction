const https = require('node:https');

const baseUrl = process.env.BASE_URL ?? 'https://localhost:7000';
const authorId = process.env.SSP_AUTHOR_ID ?? '01K52M35BFK3ED1HB7V7FY353C';
const storyId = process.env.SSP_STORY_ID ?? '01K6S20WDTEWH7Q3MRJM0G9MRH';
const chapterStoryId = process.env.SSP_CHAPTER_STORY_ID ?? '01K6SFB0G8EQ5MVNZ0RC6V1QCZ';
const chapterId = process.env.SSP_CHAPTER_ID ?? '01K7J887GFPBQEJKE0Z6ACMP1Z';

const routes = [
  '/',
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
      rejectUnauthorized: false,
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
