import { expect, type Page, type Request, type Response } from '@playwright/test';

// The browser SDK reports its own events during navigation. These requests are
// unrelated to the application queries that the navigation tests monitor.
export function isApplicationApiRequest(request: Request): boolean {
    const path = new URL(request.url()).pathname;
    if (!path.startsWith('/api/v2/')) {
        return false;
    }

    return !((request.method() === 'POST' && path === '/api/v2/events') || (request.method() === 'GET' && path === '/api/v2/events/session/heartbeat'));
}

export function trackPendingApiRequests(page: Page): { waitForPendingRequests: () => Promise<void> } {
    const pendingRequests = new Set<Request>();
    page.on('request', (request) => {
        if (isApplicationApiRequest(request)) {
            pendingRequests.add(request);
        }
    });
    page.on('requestfinished', (request) => pendingRequests.delete(request));
    page.on('requestfailed', (request) => pendingRequests.delete(request));

    return {
        waitForPendingRequests: async () => {
            await expect
                .poll(() => Array.from(pendingRequests, (request) => `${request.method()} ${request.url()}`), {
                    message: 'Application API requests should finish before the next full-page navigation',
                    timeout: 30_000
                })
                .toEqual([]);
        }
    };
}

export function waitForEventDetailResponses(page: Page, eventId: string, stackId: string, projectId: string): Promise<Response[]> {
    const eventResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === `/api/v2/events/${eventId}` && url.searchParams.get('expected_stack_id') === stackId;
    });
    return Promise.all([eventResponse, waitForProjectUserCountResponse(page, projectId)]);
}

export function waitForProjectUserCountResponse(page: Page, projectId: string): Promise<Response> {
    return page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === `/api/v2/projects/${projectId}/events/count` && url.searchParams.get('aggregations') === 'cardinality:user';
    });
}
