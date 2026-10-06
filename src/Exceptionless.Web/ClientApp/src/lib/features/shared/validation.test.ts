import { describe, expect, it } from 'vitest';

import { getProblemMessage, problemDetailsToFormErrors } from './validation';

describe('getProblemMessage', () => {
    it('returns the title from a plain problem details payload', () => {
        const problem = {
            instance: 'DELETE /api/v2/organizations/6a121886ad40fc0017a40d3c',
            status: 400,
            title: 'An organization cannot be deleted if it has a subscription.',
            traceId: '00-69cdfbe265cc359bc13ad2ca1448bd44-32513e821c2df6fa-00',
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1'
        };

        expect(getProblemMessage(problem, 'Please try again.')).toBe('An organization cannot be deleted if it has a subscription.');
    });

    it('prefers validation errors when present', () => {
        const problem = {
            errors: {
                general: ['The uploaded file is too large.']
            },
            status: 422,
            title: 'Validation failed.'
        };

        expect(getProblemMessage(problem, 'Please try again.')).toBe('The uploaded file is too large.');
    });

    it('falls back when the error is not problem details shaped', () => {
        expect(getProblemMessage(new Error('boom'), 'Please try again.')).toBe('Please try again.');
    });
});

describe('problemDetailsToFormErrors', () => {
    it.each([{ status: 400 }, { status: 422 }, { errors: {}, status: 422 }, { errors: { version: [] }, status: 422 }])(
        'shows the problem message when there are no field errors: %j',
        (details) => {
            const problem = {
                ...details,
                title: 'An organization cannot be deleted if it has a subscription.'
            };

            expect(problemDetailsToFormErrors(problem as never)).toEqual({
                form: 'An organization cannot be deleted if it has a subscription.'
            });
        }
    );

    it('preserves specific field errors without adding the generic title', () => {
        expect(
            problemDetailsToFormErrors({
                errors: { version: ['Version is invalid.'] },
                status: 422,
                title: 'Validation failed.'
            } as never)
        ).toEqual({ fields: { version: 'Version is invalid.' } });
    });
});
