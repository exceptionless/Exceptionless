import { type Link, parseLinkHeader } from '@web3-storage/parse-link-header';

export function parsePreviousPageQueryParameters(
	linkHeader?: string | null
): Record<string, unknown> | undefined {
	if (!linkHeader) {
		return;
	}

	return getQueryParametersFromLink(parseLinkHeader(linkHeader)?.previous);
}

export function parseNextPageQueryParameters(
	linkHeader?: string | null
): Record<string, unknown> | undefined {
	if (!linkHeader) {
		return;
	}

	return getQueryParametersFromLink(parseLinkHeader(linkHeader)?.next);
}

function getQueryParametersFromLink(link?: Link): Record<string, unknown> | undefined {
	if (!link) {
		return;
	}

	// eslint-disable-next-line @typescript-eslint/no-unused-vars
	const { url, rel, ...params } = link;
	return Object.keys(params).length > 0 ? params : undefined;
}
