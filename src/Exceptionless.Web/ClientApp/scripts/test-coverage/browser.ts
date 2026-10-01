import type { Browser, BrowserContext, Page } from '@playwright/test';
import type { SourceCoverageData } from '@vitest/istanbul-lib-instrument';

import { isDeepStrictEqual } from 'node:util';

export type RawCoverage = Record<string, SourceCoverageData & { hash?: string }>;
interface Payload {
    coverage?: RawCoverage;
    id: string;
    kind: 'end' | 'start';
}

export class BrowserCoverage {
    public readonly coverage: RawCoverage = {};
    public readonly documents = new Map<string, boolean>();
    public readonly errors: string[] = [];
    private readonly contexts = new WeakMap<BrowserContext, Promise<void>>();
    private readonly pages = new WeakMap<Page, Promise<void>>();
    private restore: (() => void) | undefined;

    public async finish(browser: Browser) {
        try {
            for (const context of browser.contexts()) await context.close();
        } catch (error) {
            this.errors.push(String(error));
        } finally {
            this.restore?.();
        }
        const unfinished = [...this.documents.values()].filter((ended) => !ended).length;
        if (unfinished) this.errors.push(`${unfinished} instrumented documents did not finalize coverage`);
        if (!Object.keys(this.coverage).length) this.errors.push('No application browser coverage was collected');
        return {
            complete: this.errors.length === 0,
            coverage: this.coverage,
            documents: this.documents.size,
            errors: this.errors
        };
    }

    public async install(browser: Browser) {
        const newContext = browser.newContext.bind(browser);
        const newPage = browser.newPage.bind(browser);
        browser.newContext = async (options) => {
            const context = await newContext(options);
            await this.attachContext(context);
            return context;
        };
        browser.newPage = async (options) => {
            const page = await newPage(options);
            await this.attachContext(page.context());
            await this.attachPage(page);
            return page;
        };
        this.restore = () => {
            browser.newContext = newContext;
            browser.newPage = newPage;
        };
        for (const context of browser.contexts()) await this.attachContext(context);
    }

    private attachContext(context: BrowserContext) {
        const existing = this.contexts.get(context);
        if (existing) return existing;
        const ready = (async () => {
            await context.exposeBinding('__exceptionlessCoverageReport', (_source, payload: Payload) => {
                try {
                    this.record(payload);
                } catch (error) {
                    this.errors.push(String(error));
                }
            });
            await context.addInitScript(installDocumentCoverage);
            context.on('page', (page) => {
                void this.attachPage(page).catch((error) => this.errors.push(String(error)));
            });
            const newPage = context.newPage.bind(context);
            context.newPage = async () => {
                const page = await newPage();
                await this.attachPage(page);
                return page;
            };
            for (const page of context.pages()) await this.attachPage(page);
            const close = context.close.bind(context);
            context.close = async (options) => {
                for (const page of context.pages()) await this.flush(page);
                return close(options);
            };
        })();
        this.contexts.set(context, ready);
        return ready;
    }

    private attachPage(page: Page): Promise<void> {
        const existing = this.pages.get(page);
        if (existing) return existing;
        const ready = (async () => {
            const session = await page.context().newCDPSession(page);
            session.on('Runtime.bindingCalled', (event) => {
                if (event.name !== '__exceptionlessCoverageMessage') return;
                try {
                    this.record(JSON.parse(event.payload) as Payload);
                } catch (error) {
                    this.errors.push(String(error));
                }
            });
            await session.send('Runtime.enable');
            await session.send('Runtime.addBinding', { name: '__exceptionlessCoverageMessage' });
            const close = page.close.bind(page);
            page.close = async (options) => {
                await this.flush(page);
                return close(options);
            };
        })();
        this.pages.set(page, ready);
        return ready;
    }

    private async flush(page: Page) {
        for (const frame of page.frames()) {
            try {
                await frame.evaluate(async () => {
                    const target = globalThis as typeof globalThis & { __exceptionlessFlushCoverage?: () => Promise<void> };
                    await target.__exceptionlessFlushCoverage?.();
                });
            } catch (error) {
                // A detached document must already have sent its pagehide report.
                // finish() rejects any registered document that did not finalize.
                if (!frame.isDetached() && !page.isClosed()) this.errors.push(String(error));
            }
        }
    }

    private record(payload: Payload) {
        if (!payload.id || !['end', 'start'].includes(payload.kind)) throw new Error('Malformed browser coverage message');
        if (payload.kind === 'start') {
            this.documents.set(payload.id, false);
            return;
        }
        if (!this.documents.has(payload.id)) throw new Error('Coverage document was not registered');
        if (!payload.coverage) throw new Error('Coverage document lost its counters');
        for (const [path, file] of Object.entries(payload.coverage)) {
            const previous = this.coverage[path];
            if (
                previous &&
                (!isDeepStrictEqual(previous.statementMap, file.statementMap) ||
                    !isDeepStrictEqual(previous.fnMap, file.fnMap) ||
                    !isDeepStrictEqual(previous.branchMap, file.branchMap) ||
                    previous.hash !== file.hash)
            ) {
                throw new Error(`Application changed during browser coverage: ${path}`);
            }
            const next = previous ?? structuredClone(file);
            for (const kind of ['s', 'f'] as const) {
                for (const [key, value] of Object.entries(file[kind])) {
                    if (!Number.isFinite(value) || value < 0) throw new Error('Invalid coverage counter');
                    next[kind][key] = Number(value > 0 || (previous?.[kind][key] ?? 0) > 0);
                }
            }
            for (const [key, values] of Object.entries(file.b)) {
                next.b[key] = values.map((value, index) => {
                    if (!Number.isFinite(value) || value < 0) throw new Error('Invalid branch counter');
                    return Number(value > 0 || (previous?.b[key]?.[index] ?? 0) > 0);
                });
            }
            this.coverage[path] = next;
        }
        this.documents.set(payload.id, true);
    }
}

// This function is serialized by Playwright and runs before application scripts,
// including in popups, navigated documents, and newly created frames.
function installDocumentCoverage() {
    const target = globalThis as typeof globalThis & {
        __coverage__?: RawCoverage;
        __exceptionlessCoverageMessage?: (payload: string) => void;
        __exceptionlessCoverageReport: (payload: Payload) => Promise<void>;
        __exceptionlessFlushCoverage?: () => Promise<void>;
    };
    const id = crypto.randomUUID();
    let coverage: RawCoverage | undefined;
    let started = false;
    const send = (kind: Payload['kind']) => {
        const payload = { coverage: kind === 'end' ? coverage : undefined, id, kind };
        if (target.__exceptionlessCoverageMessage) {
            target.__exceptionlessCoverageMessage(JSON.stringify(payload));
            return Promise.resolve();
        }
        return target.__exceptionlessCoverageReport(payload);
    };

    // Do not register empty/third-party documents. Registration begins only when
    // the opted-in application instrumenter creates its coverage store.
    Object.defineProperty(target, '__coverage__', {
        configurable: true,
        get: () => coverage,
        set(value: RawCoverage) {
            coverage = value;
            if (!started) {
                started = true;
                void send('start').catch(() => {});
            }
        }
    });
    target.__exceptionlessFlushCoverage = () => (started ? send('end') : Promise.resolve());
    addEventListener('beforeunload', () => {
        if (started) void send('end').catch(() => {});
    });
    addEventListener('pagehide', () => {
        if (started) void send('end').catch(() => {});
    });
    addEventListener('pageshow', (event) => {
        if (started && event.persisted) void send('start').catch(() => {});
    });
}
