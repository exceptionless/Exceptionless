import type { FetchClientResponse } from '@exceptionless/fetchclient';

import { ProblemDetails } from '@exceptionless/fetchclient';
import { describe, expect, it } from 'vitest';

import { normalizeDeserializationFailure } from './fetch-client-response';

describe('normalizeDeserializationFailure', () => {
    it('leaves successful response data unchanged', () => {
        const response = createResponse({ value: 42 });

        expect(normalizeDeserializationFailure(response)).toBe(response);
    });

    it('turns an aborted successful body read into a client-closed error response', () => {
        const problem = new ProblemDetails().setErrorMessage('Unable to deserialize response data: Failed to fetch');
        problem.title = 'Unable to deserialize response data: Failed to fetch';
        const response = createResponse(problem);

        const normalized = normalizeDeserializationFailure(response);

        expect(normalized?.ok).toBe(false);
        expect(normalized?.status).toBe(499);
        expect(normalized?.data).toBeNull();
        expect(normalized?.problem).toBe(problem);
        expect(normalized?.meta).toBe(response.meta);
    });

    it('turns malformed successful JSON into a retryable gateway error response', () => {
        const problem = new ProblemDetails().setErrorMessage('Unable to deserialize response data: Unexpected token');
        problem.title = 'Unable to deserialize response data: Unexpected token';
        const response = createResponse(problem);

        const normalized = normalizeDeserializationFailure(response);

        expect(normalized?.ok).toBe(false);
        expect(normalized?.status).toBe(502);
        expect(normalized?.problem.status).toBe(502);
    });
});

function createResponse(data: unknown): FetchClientResponse<unknown> {
    const response = new Response('{}', { status: 200 }) as FetchClientResponse<unknown>;
    response.data = data;
    response.meta = { links: {} };
    response.problem = new ProblemDetails();

    return response;
}
