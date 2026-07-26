import { spawn, type ChildProcess } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { parse, type DefaultTreeAdapterMap } from 'parse5';
import { parityScenarios } from './stories/parity-scenarios.js';

type Node = DefaultTreeAdapterMap['node'];
type Element = DefaultTreeAdapterMap['element'];
type TextNode = DefaultTreeAdapterMap['textNode'];
type CdpResponse = { id?: number; method?: string; result?: unknown; error?: { message: string } };
type PixelResult = { mismatchedPixels: number; maxChannelDelta: number; bounds: string; diff: string };
type ScreenshotResult = { data: string; contentHeight: number };

const ignoredElements = new Set(['head', 'script', 'style', 'title']);
const semanticOnly = process.argv.includes('--semantic-only');
const debugScenario = process.env.EMAIL_PARITY_DEBUG;
const scenarioFilter = process.env.EMAIL_PARITY_SCENARIO ?? debugScenario;
const artifactsDirectory = resolve('parity-artifacts');
const comparisonWidths = [800, 375];
const scenarios = scenarioFilter
    ? parityScenarios.filter((scenario) => scenario.id === scenarioFilter)
    : parityScenarios;

if (scenarios.length === 0) {
    throw new Error(`Unknown parity scenario: ${scenarioFilter}`);
}

function isElement(node: Node): node is Element {
    return 'tagName' in node;
}

function isTextNode(node: Node): node is TextNode {
    return node.nodeName === '#text';
}

function normalizeText(value: string): string {
    return value
        .replace(/\u00a0/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

function textContent(node: Node, ignored = false): string {
    if (isTextNode(node)) {
        return ignored ? '' : node.value;
    }

    const shouldIgnore = ignored || (isElement(node) && ignoredElements.has(node.tagName));
    return 'childNodes' in node ? node.childNodes.map((child) => textContent(child, shouldIgnore)).join(' ') : '';
}

function getAttribute(element: Element, name: string): string {
    return element.attrs.find((attribute) => attribute.name === name)?.value ?? '';
}

function actionLabel(element: Element): string {
    const text = normalizeText(textContent(element));
    if (text) {
        return text;
    }

    const image = element.childNodes.find((child): child is Element => isElement(child) && child.tagName === 'img');
    return image ? getAttribute(image, 'alt') : '';
}

function normalizeHref(value: string): string {
    if (!value.startsWith('mailto:')) {
        return value;
    }

    try {
        return decodeURIComponent(value);
    } catch {
        return value;
    }
}

function collectActions(node: Node, actions: string[] = []): string[] {
    if (isElement(node) && node.tagName === 'a') {
        actions.push(`${actionLabel(node)} → ${normalizeHref(getAttribute(node, 'href'))}`);
    }

    if ('childNodes' in node) {
        node.childNodes.forEach((child) => collectActions(child, actions));
    }

    return actions;
}

function getSemanticContract(html: string): { text: string; actions: string[] } {
    const document = parse(html);
    return {
        text: normalizeText(textContent(document)),
        actions: collectActions(document)
    };
}

function firstDifference(left: string, right: string): string {
    let index = 0;
    while (index < left.length && index < right.length && left[index] === right[index]) {
        index++;
    }

    const start = Math.max(0, index - 80);
    const end = index + 160;
    return [`legacy: ${left.slice(start, end)}`, `modern: ${right.slice(start, end)}`].join('\n');
}

class ChromeDevTools {
    private nextId = 1;
    private readonly pending = new Map<
        number,
        { resolve: (value: unknown) => void; reject: (reason?: unknown) => void }
    >();
    private readonly waiting = new Map<string, Array<() => void>>();

    private constructor(private readonly socket: WebSocket) {
        socket.addEventListener('message', (event) => {
            const message = JSON.parse(String(event.data)) as CdpResponse;
            if (message.id) {
                const request = this.pending.get(message.id);
                if (!request) return;
                this.pending.delete(message.id);
                if (message.error) request.reject(new Error(message.error.message));
                else request.resolve(message.result);
                return;
            }

            if (message.method) {
                const listeners = this.waiting.get(message.method) ?? [];
                this.waiting.delete(message.method);
                listeners.forEach((listener) => listener());
            }
        });
    }

    static async connect(url: string): Promise<ChromeDevTools> {
        const socket = new WebSocket(url);
        await new Promise<void>((resolveConnection, rejectConnection) => {
            socket.addEventListener('open', () => resolveConnection(), { once: true });
            socket.addEventListener('error', () => rejectConnection(new Error('Unable to connect to Chromium')), {
                once: true
            });
        });
        return new ChromeDevTools(socket);
    }

    send(method: string, params: Record<string, unknown> = {}): Promise<unknown> {
        const id = this.nextId++;
        const response = new Promise<unknown>((resolveRequest, rejectRequest) => {
            this.pending.set(id, { resolve: resolveRequest, reject: rejectRequest });
        });
        this.socket.send(JSON.stringify({ id, method, params }));
        return response;
    }

    waitFor(method: string): Promise<void> {
        return new Promise((resolveEvent) => {
            const listeners = this.waiting.get(method) ?? [];
            listeners.push(resolveEvent);
            this.waiting.set(method, listeners);
        });
    }

    close(): void {
        this.socket.close();
    }
}

function findChrome(): string {
    const candidates = [
        process.env.CHROME_PATH,
        '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
        '/usr/bin/google-chrome-stable',
        '/usr/bin/google-chrome',
        '/usr/bin/chromium',
        '/usr/bin/chromium-browser'
    ].filter((candidate): candidate is string => Boolean(candidate));
    const executable = candidates.find((candidate) => existsSync(candidate));
    if (!executable) {
        throw new Error('Chromium was not found. Set CHROME_PATH to run pixel parity validation.');
    }
    return executable;
}

async function launchChrome(): Promise<{
    client: ChromeDevTools;
    process: ChildProcess;
    userDataDirectory: string;
}> {
    const userDataDirectory = mkdtempSync(join(tmpdir(), 'exceptionless-email-parity-'));
    const chromeProcess = spawn(
        findChrome(),
        [
            '--headless=new',
            '--disable-background-networking',
            '--disable-breakpad',
            '--disable-component-update',
            '--disable-dev-shm-usage',
            '--disable-gpu',
            '--hide-scrollbars',
            '--no-first-run',
            '--no-sandbox',
            '--remote-debugging-port=0',
            `--user-data-dir=${userDataDirectory}`,
            'about:blank'
        ],
        { stdio: ['ignore', 'ignore', 'pipe'] }
    );

    const browserWebSocketUrl = await new Promise<string>((resolveUrl, rejectUrl) => {
        const timeout = setTimeout(() => rejectUrl(new Error('Timed out waiting for Chromium DevTools')), 15_000);
        chromeProcess.stderr?.on('data', (data: Buffer) => {
            const match = String(data).match(/DevTools listening on (ws:\/\/\S+)/);
            if (match) {
                clearTimeout(timeout);
                resolveUrl(match[1]);
            }
        });
        chromeProcess.once('exit', (code) => {
            clearTimeout(timeout);
            rejectUrl(new Error(`Chromium exited before startup (code ${code ?? 'unknown'})`));
        });
    });

    const browserUrl = new URL(browserWebSocketUrl);
    const target = (await fetch(`http://${browserUrl.host}/json/new?about:blank`, { method: 'PUT' }).then((response) =>
        response.json()
    )) as { webSocketDebuggerUrl: string };
    const client = await ChromeDevTools.connect(target.webSocketDebuggerUrl);
    await client.send('Page.enable');
    return { client, process: chromeProcess, userDataDirectory };
}

function prepareForScreenshot(html: string, logoDataUrl: string): string {
    return html.replaceAll('https://be.exceptionless.io/img/exceptionless-logo.png', logoDataUrl);
}

async function screenshot(
    client: ChromeDevTools,
    html: string,
    width: number,
    height: number
): Promise<ScreenshotResult> {
    await client.send('Emulation.setDeviceMetricsOverride', {
        width,
        height,
        deviceScaleFactor: 1,
        mobile: false
    });
    const loaded = client.waitFor('Page.loadEventFired');
    await client.send('Page.navigate', { url: `data:text/html;base64,${Buffer.from(html).toString('base64')}` });
    await loaded;
    await client.send('Runtime.evaluate', {
        expression:
            'Promise.all([...document.images].map((image) => image.complete ? true : new Promise((resolve) => { image.onload = image.onerror = resolve; })))',
        awaitPromise: true
    });
    const dimensions = (await client.send('Runtime.evaluate', {
        expression: 'Math.max(document.body.scrollHeight, document.documentElement.scrollHeight)',
        returnByValue: true
    })) as { result: { value: number } };
    const result = (await client.send('Page.captureScreenshot', {
        format: 'png',
        fromSurface: true,
        clip: { x: 0, y: 0, width, height, scale: 1 }
    })) as { data: string };
    return { data: result.data, contentHeight: dimensions.result.value };
}

async function comparePixels(client: ChromeDevTools, legacy: string, modern: string): Promise<PixelResult> {
    const expression = `
        (async () => {
            const load = (source) => new Promise((resolve, reject) => {
                const image = new Image();
                image.onload = () => resolve(image);
                image.onerror = reject;
                image.src = source;
            });
            const [legacy, modern] = await Promise.all([
                load("data:image/png;base64,${legacy}"),
                load("data:image/png;base64,${modern}")
            ]);
            const canvas = document.createElement("canvas");
            canvas.width = legacy.width;
            canvas.height = legacy.height;
            const context = canvas.getContext("2d", { willReadFrequently: true });
            context.drawImage(legacy, 0, 0);
            const before = context.getImageData(0, 0, canvas.width, canvas.height).data;
            context.clearRect(0, 0, canvas.width, canvas.height);
            context.drawImage(modern, 0, 0);
            const after = context.getImageData(0, 0, canvas.width, canvas.height).data;
            const difference = context.createImageData(canvas.width, canvas.height);
            difference.data.fill(255);
            let mismatchedPixels = 0;
            let maxChannelDelta = 0;
            let minX = canvas.width;
            let minY = canvas.height;
            let maxX = -1;
            let maxY = -1;
            for (let index = 0; index < before.length; index += 4) {
                let changed = false;
                for (let channel = 0; channel < 4; channel++) {
                    const delta = Math.abs(before[index + channel] - after[index + channel]);
                    maxChannelDelta = Math.max(maxChannelDelta, delta);
                    changed ||= delta !== 0;
                }
                if (changed) {
                    mismatchedPixels++;
                    const pixel = index / 4;
                    const x = pixel % canvas.width;
                    const y = Math.floor(pixel / canvas.width);
                    minX = Math.min(minX, x);
                    minY = Math.min(minY, y);
                    maxX = Math.max(maxX, x);
                    maxY = Math.max(maxY, y);
                    difference.data[index + 1] = 0;
                    difference.data[index + 2] = 0;
                }
            }
            context.putImageData(difference, 0, 0);
            return {
                mismatchedPixels,
                maxChannelDelta,
                bounds: mismatchedPixels === 0 ? "none" : \`\${minX},\${minY}-\${maxX},\${maxY}\`,
                diff: mismatchedPixels === 0 ? "" : canvas.toDataURL("image/png").split(",")[1]
            };
        })()
    `;
    const response = (await client.send('Runtime.evaluate', {
        expression,
        awaitPromise: true,
        returnByValue: true
    })) as { result: { value: PixelResult }; exceptionDetails?: unknown };
    if (response.exceptionDetails) {
        throw new Error('Chromium could not compare screenshots');
    }
    return response.result.value;
}

async function inspectGeometry(client: ChromeDevTools): Promise<unknown> {
    const response = (await client.send('Runtime.evaluate', {
        expression: `
            [...document.querySelectorAll("img,[data-email-header],[data-email-header] table,[data-email-social],[data-social-column],h5,a[href*='facebook.com/exceptionless'],a[href*='twitter.com/exceptionless'],a[href*='github.com/exceptionless'],a[href='mailto:support@exceptionless.io'],.callout-inner,.callout-inner>b,.callout-inner>h1,[data-email-summary-metrics],[data-summary-column],[data-email-summary-stat],[data-email-summary-stat]>b,[data-email-summary-stat]>div")]
                .filter((element) => element.getBoundingClientRect().y < 1000)
                .map((element) => {
                const rect = element.getBoundingClientRect();
                const style = getComputedStyle(element);
                return {
                    text: element.textContent.trim().replace(/\\s+/g, " ").slice(0, 70),
                    tag: element.tagName,
                    rect: [rect.x, rect.y, rect.width, rect.height],
                    font: style.font,
                    lineHeight: style.lineHeight,
                    color: style.color,
                    margin: style.margin
                };
            })
        `,
        returnByValue: true
    })) as { result: { value: unknown } };
    return response.result.value;
}

let failures = 0;

for (const scenario of scenarios) {
    const legacy = getSemanticContract(scenario.legacyHtml);
    const modern = getSemanticContract(scenario.modernHtml);
    const textMatches = legacy.text === modern.text;
    const actionsMatch = JSON.stringify(legacy.actions) === JSON.stringify(modern.actions);

    if (textMatches && actionsMatch) {
        console.log(`PASS ${scenario.id}: text and ${modern.actions.length} actions match`);
        continue;
    }

    failures++;
    console.error(`FAIL ${scenario.id}`);

    if (!textMatches) {
        console.error(firstDifference(legacy.text, modern.text));
    }

    if (!actionsMatch) {
        console.error('legacy actions:', JSON.stringify(legacy.actions, null, 2));
        console.error('modern actions:', JSON.stringify(modern.actions, null, 2));
    }
}

if (failures > 0) {
    console.error(`\n${failures} of ${scenarios.length} parity scenarios failed.`);
    process.exitCode = 1;
} else {
    console.log(`\nAll ${scenarios.length} parity scenarios preserve exact text and actions.`);
}

if (!semanticOnly) {
    rmSync(artifactsDirectory, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
    const logo = readFileSync(resolve('../Exceptionless.Web/ClientApp.angular/img/exceptionless-logo.png'));
    const logoDataUrl = `data:image/png;base64,${logo.toString('base64')}`;
    const chrome = await launchChrome();

    try {
        let pixelFailures = 0;
        for (const scenario of scenarios) {
            for (const width of comparisonWidths) {
                const legacyHtml = prepareForScreenshot(scenario.legacyHtml, logoDataUrl);
                const modernHtml = prepareForScreenshot(scenario.modernHtml, logoDataUrl);
                let legacy = await screenshot(chrome.client, legacyHtml, width, scenario.height);
                const legacyGeometry = debugScenario === scenario.id ? await inspectGeometry(chrome.client) : undefined;
                let modern = await screenshot(chrome.client, modernHtml, width, scenario.height);
                const fullHeight = Math.max(scenario.height, legacy.contentHeight, modern.contentHeight);
                if (fullHeight > scenario.height) {
                    legacy = await screenshot(chrome.client, legacyHtml, width, fullHeight);
                    modern = await screenshot(chrome.client, modernHtml, width, fullHeight);
                }
                if (debugScenario === scenario.id) {
                    console.log(
                        JSON.stringify(
                            { width, legacy: legacyGeometry, modern: await inspectGeometry(chrome.client) },
                            null,
                            2
                        )
                    );
                }
                const result = await comparePixels(chrome.client, legacy.data, modern.data);
                if (result.mismatchedPixels === 0) {
                    console.log(`PASS ${scenario.id}: exact pixel match at ${width}×${fullHeight}px`);
                    continue;
                }

                pixelFailures++;
                mkdirSync(artifactsDirectory, { recursive: true });
                writeFileSync(
                    join(artifactsDirectory, `${scenario.id}-${width}-legacy.png`),
                    Buffer.from(legacy.data, 'base64')
                );
                writeFileSync(
                    join(artifactsDirectory, `${scenario.id}-${width}-modern.png`),
                    Buffer.from(modern.data, 'base64')
                );
                writeFileSync(
                    join(artifactsDirectory, `${scenario.id}-${width}-diff.png`),
                    Buffer.from(result.diff, 'base64')
                );
                console.error(
                    `FAIL ${scenario.id} at ${width}px: ${result.mismatchedPixels} pixels differ, max channel delta ${result.maxChannelDelta}, bounds ${result.bounds}`
                );
            }
        }

        if (pixelFailures > 0) {
            console.error(
                `\n${pixelFailures} of ${scenarios.length} pixel scenarios failed. See ${artifactsDirectory}.`
            );
            process.exitCode = 1;
        } else {
            console.log(`\nAll ${scenarios.length} scenarios are exact pixel matches.`);
        }
    } finally {
        chrome.client.close();
        chrome.process.kill();
        rmSync(chrome.userDataDirectory, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
    }
}
