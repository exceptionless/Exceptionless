import { expect, type Page, test } from '@playwright/test';

// Browser updates can change how CSP directives apply (Chrome 158 started blocking the hashed
// SvelteKit bootstrap's import() calls under 'strict-dynamic'). Only the published build served
// by ASP.NET sends the policy header, so this runs on a schedule against deployed environments in
// Chrome stable and beta (see .github/workflows/browser-compatibility-monitoring.yml); it skips
// under the Vite dev server.

// Scalar bundles Zod 4, which probes for eval support with try { Function('') } and falls back.
// Our own app avoids the probe with Zod's jitless config (hooks.client.ts).
const knownBenignViolations = [/^script-src blocked eval \(.*\/docs\/scalar\.js:/];

interface PageProblems {
    errors: string[];
    violations: string[];
}

async function gotoWithPolicy(page: Page, path: string) {
    const response = await page.goto(path);
    expect(response?.ok()).toBe(true);
    test.skip(!response?.headers()['content-security-policy'], 'No CSP header: not the published build served by ASP.NET.');
}

async function watchForProblems(page: Page): Promise<PageProblems> {
    const problems: PageProblems = { errors: [], violations: [] };
    page.on('pageerror', (error) => problems.errors.push(error.message));
    await page.exposeFunction('__reportCspViolation', (violation: string) => {
        if (!knownBenignViolations.some((pattern) => pattern.test(violation))) {
            problems.violations.push(violation);
        }
    });
    await page.addInitScript(() => {
        document.addEventListener('securitypolicyviolation', (event) => {
            const report = (window as unknown as { __reportCspViolation: (violation: string) => void }).__reportCspViolation;
            void report(`${event.effectiveDirective} blocked ${event.blockedURI} (${event.sourceFile}:${event.lineNumber})`);
        });
    });

    return problems;
}

test('published app boots under its content security policy @csp', async ({ page }) => {
    // Arrange
    const problems = await watchForProblems(page);

    // Act
    await gotoWithPolicy(page, '/login');

    // Assert
    await expect.soft(page.getByLabel('Email', { exact: true })).toBeVisible({ timeout: 30_000 });
    expect(problems).toEqual({ errors: [], violations: [] });
});

test('API reference loads under its content security policy @csp', async ({ page }) => {
    // Arrange
    const problems = await watchForProblems(page);

    // Act
    await gotoWithPolicy(page, '/docs');

    // Assert
    await expect.soft(page.getByText('Exceptionless API').first()).toBeVisible({ timeout: 30_000 });
    expect(problems).toEqual({ errors: [], violations: [] });
});

test('Stripe payment element mounts under the app content security policy @csp', async ({ page }) => {
    // Arrange
    const problems = await watchForProblems(page);

    // Act
    await gotoWithPolicy(page, '/login');

    // Assert
    await expect(page.getByLabel('Email', { exact: true }), `App did not boot: ${JSON.stringify(problems)}`).toBeVisible({ timeout: 30_000 });

    // Act
    // Loads Stripe.js the way @stripe/stripe-js does (an injected script) and mounts the Payment
    // Element, without needing a signed-in organization.
    const result = await page.evaluate(async () => {
        const { env } = (await import(/* @vite-ignore */ '/_app/env.js')) as { env: Record<string, string | undefined> };
        const key = env.PUBLIC_STRIPE_PUBLISHABLE_KEY;
        if (!key) {
            return { configured: false, frames: 0 };
        }

        await new Promise<void>((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://js.stripe.com/v3/';
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Stripe.js failed to load'));
            document.head.append(script);
        });

        type StripeElement = { mount(target: HTMLElement): void; on(event: 'ready', handler: () => void): void };
        type StripeFactory = (key: string) => { elements(options: object): { create(type: 'payment'): StripeElement } };
        const stripe = (window as unknown as { Stripe: StripeFactory }).Stripe(key);
        const container = document.createElement('div');
        document.body.append(container);
        const paymentElement = stripe.elements({ amount: 1500, currency: 'usd', mode: 'subscription' }).create('payment');
        await new Promise<void>((resolve) => {
            paymentElement.on('ready', () => resolve());
            paymentElement.mount(container);
        });

        return { configured: true, frames: container.querySelectorAll('iframe').length };
    });

    // Assert
    test.skip(!result.configured, 'Stripe is not configured for this environment.');
    expect(result.frames).toBeGreaterThan(0);
    expect(problems).toEqual({ errors: [], violations: [] });
});
