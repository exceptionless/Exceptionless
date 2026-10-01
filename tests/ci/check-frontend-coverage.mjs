// A small collector contract, not an additional application journey.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { BrowserCoverage } from '../../src/Exceptionless.Web/ClientApp/scripts/test-coverage/browser.ts';
import { frontendCoverage } from '../../src/Exceptionless.Web/ClientApp/scripts/test-coverage/vite.ts';
import { normalizeFile, union } from './frontend-coverage.mjs';

const client = resolve(import.meta.dirname, '../../src/Exceptionless.Web/ClientApp');
const fixture = join(client, 'scripts/test-coverage/fixtures');
const output = mkdtempSync(join(process.env.TMPDIR ?? tmpdir(), 'exceptionless-frontend-contract-'));
const require = createRequire(join(client, 'package.json'));
const { createServer } = require('vite');
const { chromium } = require('@playwright/test');
const { createCoverageMap } = require('@vitest/istanbul-lib-coverage');
const { createSourceMapStore } = require('@vitest/istanbul-lib-source-maps');
execFileSync('npm', ['run', 'test:unit', '--', '--config', join(fixture, 'vite.config.ts'), '--coverage', `--coverage.reportsDirectory=${join(output, 'unit')}`], {
    cwd: client, stdio: 'inherit', timeout: 60_000
});
const units = JSON.parse(readFileSync(join(output, 'unit/coverage-final.json'), 'utf8'));
assert.equal(createCoverageMap(units).fileCoverageFor(join(fixture, 'src/exercise.ts')).getLineCoverage()[8], 0, 'Throwing parse must not cover its return');
assert.ok(Object.values(units[join(fixture, 'src/untouched.ts')].s).every((hit) => hit === 0), 'Unimported source must have zero counters');

const server = await createServer({
    configFile: join(fixture, 'vite.config.ts'),
    server: { host: '127.0.0.1', port: 0, strictPort: false },
    plugins: [
        frontendCoverage(),
        {
            name: 'coverage-contract-page',
            configureServer(server) {
                server.middlewares.use('/contract', async (_request, response) => {
                    response.setHeader('Content-Type', 'text/html');
                    response.end(await server.transformIndexHtml('/contract', '<main></main><script type="module" src="/contract-entry.js"></script>'));
                });
            },
            resolveId: (id) => id === '/contract-entry.js' ? id : undefined,
            load: (id) => id === '/contract-entry.js' ? `
                import { mount } from 'svelte';
                import Card from '/src/card.svelte';
                import { choose, parse } from '/src/exercise.ts';
                window.result = choose(false) + ':' + parse('{"ok":true}').ok;
                mount(Card, { target: document.querySelector('main'), props: { flag: false } });
                window.ready = true;
            ` : undefined
        }
    ]
});
let browser;
try {
    await server.listen();
    const url = `http://localhost:${server.httpServer.address().port}/contract`;
    browser = await chromium.launch();
    const collector = new BrowserCoverage();
    await collector.install(browser);
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.goto(url);
    await page.waitForFunction(() => window.ready);
    assert.equal(await page.locator('main').innerText(), 'right');
    assert.equal(await page.evaluate(() => window.result), 'right:true');
    await page.reload();
    await page.waitForFunction(() => window.ready);
    await page.goto(url + '?full-navigation');
    await page.waitForFunction(() => window.ready);
    const popupReady = page.waitForEvent('popup');
    await page.evaluate((url) => window.open(url), url);
    const popup = await popupReady;
    await popup.waitForFunction(() => window.ready);
    const popupClosed = popup.waitForEvent('close');
    await popup.evaluate(() => window.close());
    await popupClosed;
    const early = await context.newPage();
    await early.goto(url);
    await early.waitForFunction(() => window.ready);
    await early.close();
    const manual = await browser.newContext();
    const manualPage = await manual.newPage();
    await manualPage.goto(url);
    await manualPage.waitForFunction(() => window.ready);
    await manual.close();
    const standalone = await browser.newPage();
    await standalone.goto(url);
    await standalone.waitForFunction(() => window.ready);
    await standalone.close();
    const result = await collector.finish(browser);
    assert.equal(result.complete, true, result.errors.join('; '));
    assert.equal(result.documents, 7);
    writeFileSync(join(output, 'browser-raw.json'), JSON.stringify(result));
    const mapped = (await createSourceMapStore().transformCoverage(createCoverageMap(result.coverage))).toJSON();
    const normalize = (report) => Object.fromEntries(Object.entries(report).map(([path, file]) => [
        path, normalizeFile(file, path, readFileSync(path, 'utf8'))
    ]));
    const combined = union([normalize(units), normalize(mapped)]);
    const source = combined[join(fixture, 'src/exercise.ts')];
    assert.ok(Object.values(source.s).every((hit) => hit === 1), 'The browser must cover the opposite arm and successful parse return');
    assert.deepEqual(Object.values(source.b), [[1, 1]]);
    const card = combined[join(fixture, 'src/card.svelte')];
    const condition = Object.entries(card.branchMap).find(([, branch]) => branch.type === 'cond-expr');
    assert.ok(condition, 'Svelte script condition must map to original source');
    assert.equal(condition[1].loc.start.line, 3);
    assert.deepEqual(card.b[condition[0]], [1, 1]);
    console.log(`Frontend collector contract passed; seven documents finalized. Evidence: ${output}`);
} finally {
    await browser?.close();
    await server.close();
}
