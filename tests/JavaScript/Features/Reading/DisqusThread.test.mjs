// Run: node --test tests/JavaScript/Features/Reading/DisqusThread.test.mjs
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { afterEach, test } from 'node:test';

const source = await readFile(new URL('../../../../src/lib/IHFiction.SharedWeb/Features/Reading/DisqusThread.razor.js', import.meta.url), 'utf8');
const { loadDisqus } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

afterEach(() => {
    delete globalThis.window;
    delete globalThis.document;
    delete globalThis.DISQUS;
    delete globalThis.disqus_config;
});

test('initial embed receives thread identity and preserves the script nonce', () => {
    globalThis.window = globalThis;
    const configuration = { page: {} };
    let appendedScript;
    globalThis.document = {
        createElement: () => ({ setAttribute() {} }),
        head: {
            appendChild(script) {
                // The external embed reads this global as soon as it executes.
                window.disqus_config.call(configuration);
                appendedScript = script;
            }
        }
    };

    loadDisqus('https://example.test/read/first', 'first', 'example-forum', 'test-nonce');

    assert.deepEqual(configuration.page, { url: 'https://example.test/read/first', identifier: 'first' });
    assert.equal(appendedScript.src, 'https://example-forum.disqus.com/embed.js');
    assert.equal(appendedScript.nonce, 'test-nonce');
});

test('switching threads configures the receiver supplied by Disqus on each reset', () => {
    globalThis.window = globalThis;
    const threads = [];
    globalThis.DISQUS = {
        reset(options) {
            assert.equal(options.reload, true);
            const configuration = { page: {} };
            options.config.call(configuration);
            threads.push(configuration.page);
        }
    };

    loadDisqus('https://example.test/read/first', 'first', 'example-forum');
    loadDisqus('https://example.test/read/second', 'second', 'example-forum');

    assert.deepEqual(threads, [
        { url: 'https://example.test/read/first', identifier: 'first' },
        { url: 'https://example.test/read/second', identifier: 'second' }
    ]);
});
