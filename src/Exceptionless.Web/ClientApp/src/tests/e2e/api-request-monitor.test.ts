import type { Page, Request } from '@playwright/test';

import { EventEmitter } from 'node:events';
import { expect, test } from 'vitest';

import { isApplicationApiRequest, trackPendingApiRequests } from '../../../e2e/support/api-request-monitor';

test('tracks application requests until they settle without waiting for browser telemetry', async () => {
    // Arrange
    const events = new EventEmitter();
    const page = events as unknown as Page;
    const request = (method: string, path: string) => ({ method: () => method, url: () => `https://web-ex.dev.localhost:7131${path}` }) as Request;
    const telemetry = request('POST', '/api/v2/events');
    const heartbeat = request('GET', '/api/v2/events/session/heartbeat?close=true');
    const detail = request('GET', '/api/v2/events/123456789012345678901234');
    const eventList = request('GET', '/api/v2/organizations/123/events');
    const monitor = trackPendingApiRequests(page);

    // Act
    events.emit('request', telemetry);
    events.emit('request', heartbeat);
    events.emit('request', detail);
    const waiting = monitor.waitForPendingRequests();
    let settled = false;
    void waiting.then(() => (settled = true));
    await Promise.resolve();

    // Assert
    expect(isApplicationApiRequest(telemetry)).toBe(false);
    expect(isApplicationApiRequest(heartbeat)).toBe(false);
    expect(isApplicationApiRequest(detail)).toBe(true);
    expect(isApplicationApiRequest(eventList)).toBe(true);
    expect(settled).toBe(false);

    // Act
    events.emit('requestfinished', detail);
    await waiting;

    // Assert
    expect(settled).toBe(true);

    // Act: a failed application request must also leave the pending set.
    events.emit('request', eventList);
    events.emit('requestfailed', eventList);
    await monitor.waitForPendingRequests();

    // Assert
    expect(isApplicationApiRequest(eventList)).toBe(true);
});
