import { type FetchClientResponse, ProblemDetails } from '@foundatiofx/fetchclient';

export function normalizeDeserializationFailure(response: FetchClientResponse<unknown> | null): FetchClientResponse<unknown> | null {
    const title = response?.data instanceof ProblemDetails ? response.data.title : undefined;
    if (!response?.ok || !title?.startsWith('Unable to deserialize response data:')) {
        return response;
    }

    const problem = response.data as ProblemDetails;
    const status = title.includes('Failed to fetch') ? 499 : 502;
    problem.status = status;

    const normalizedResponse = new Response(null, {
        status,
        statusText: status === 499 ? 'Client Closed Request' : 'Bad Gateway'
    }) as FetchClientResponse<unknown>;
    normalizedResponse.data = null;
    normalizedResponse.meta = response.meta;
    normalizedResponse.problem = problem;

    return normalizedResponse;
}
