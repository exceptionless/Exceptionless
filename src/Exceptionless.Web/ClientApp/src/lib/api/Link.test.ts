import { describe, it, expect } from 'vitest';
import { parseNextPageQueryParameters, parsePreviousPageQueryParameters } from './link';

describe('link', () => {
	it('should parse undefined previous link header', () => {
		// Act
		const parameters = parsePreviousPageQueryParameters(undefined);

		// Assert
		expect(parameters).toBeUndefined();
	});

	it('should parse previous link header', () => {
		// Arrange
		const linkHeader = '</api/v2/events?before=value>; rel="previous"';

		// Act
		const parameters = parsePreviousPageQueryParameters(linkHeader);

		// Assert
		expect(parameters).toStrictEqual({ before: 'value' });
	});

	it('should parse undefined next link header', () => {
		// Act
		const parameters = parseNextPageQueryParameters(undefined);

		// Assert
		expect(parameters).toBeUndefined();
	});

	it('should parse next link header', () => {
		// Arrange
		const linkHeader = '</api/v2/events?after=value>; rel="next"';

		// Act
		const parameters = parseNextPageQueryParameters(linkHeader);

		// Assert
		expect(parameters).toStrictEqual({ after: 'value' });
	});
});
