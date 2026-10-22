import { FieldApi, FormApi } from '@tanstack/form-core';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { getProblemMessage, problemDetailsToFormErrors, submitWhenValidated } from './validation';

describe('getProblemMessage', () => {
    it('GetProblemMessage_NonProblemError_ReturnsFallback', () => {
        // Arrange
        const error = new Error('boom');

        // Act
        const actual = getProblemMessage(error, 'Please try again.');

        // Assert
        expect(actual).toBe('Please try again.');
    });

    it.each([
        [{ error_description: 'OAuth description' }, 'OAuth description'],
        [{ error: 'invalid_scope' }, 'invalid_scope'],
        [{ error: 'invalid_scope', error_description: 'OAuth description', title: 'Bad Request' }, 'OAuth description'],
        [{ detail: 'Problem detail' }, 'Problem detail'],
        [{ title: 'Problem title' }, 'Problem title'],
        [{ detail: 7, error: [], error_description: {}, title: false }, 'Fallback'],
        [{ detail: 'Problem detail', error_description: '' }, 'Problem detail'],
        [null, 'Fallback'],
        ['Malformed body', 'Fallback']
    ])('GetProblemMessage_OAuthPayload_ReturnsSafeMessage: %j', (problem, expected) => {
        // Arrange
        const fallback = 'Fallback';

        // Act
        const actual = getProblemMessage(problem, fallback);

        // Assert
        expect(actual).toBe(expected);
    });

    it('GetProblemMessage_ProblemPayload_ReturnsTitle', () => {
        // Arrange
        const problem = {
            instance: 'DELETE /api/v2/organizations/6a121886ad40fc0017a40d3c',
            status: 400,
            title: 'An organization cannot be deleted if it has a subscription.',
            traceId: '00-69cdfbe265cc359bc13ad2ca1448bd44-32513e821c2df6fa-00',
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1'
        };

        // Act
        const actual = getProblemMessage(problem, 'Please try again.');

        // Assert
        expect(actual).toBe('An organization cannot be deleted if it has a subscription.');
    });

    it('GetProblemMessage_ValidationErrors_PrefersValidationMessage', () => {
        // Arrange
        const problem = {
            errors: { general: ['The uploaded file is too large.'] },
            status: 422,
            title: 'Validation failed.'
        };

        // Act
        const actual = getProblemMessage(problem, 'Please try again.');

        // Assert
        expect(actual).toBe('The uploaded file is too large.');
    });
});

describe('problemDetailsToFormErrors', () => {
    it.each([{ status: 400 }, { status: 422 }, { errors: {}, status: 422 }, { errors: { version: [] }, status: 422 }])(
        'ProblemDetailsToFormErrors_NoFieldErrors_ReturnsProblemMessage: %j',
        (details) => {
            // Arrange
            const problem = { ...details, title: 'An organization cannot be deleted if it has a subscription.' };

            // Act
            const actual = problemDetailsToFormErrors(problem as never);

            // Assert
            expect(actual).toEqual({ form: 'An organization cannot be deleted if it has a subscription.' });
        }
    );

    it('ProblemDetailsToFormErrors_SpecificFieldErrors_OmitsGenericTitle', () => {
        // Arrange
        const problem = { errors: { version: ['Version is invalid.'] }, status: 422, title: 'Validation failed.' };

        // Act
        const actual = problemDetailsToFormErrors(problem as never);

        // Assert
        expect(actual).toEqual({ fields: { version: 'Version is invalid.' } });
    });
});

describe('submitWhenValidated', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    function createEmailForm(onSubmit: (email: string) => void) {
        const form = new FormApi({
            defaultValues: { email: '' },
            onSubmit: ({ value }) => onSubmit(value.email)
        });
        form.mount();

        const field = new FieldApi({
            form,
            name: 'email',
            validators: {
                onChangeAsync: async ({ value }: { value: string }) => (value === 'taken@example.test' ? 'Email already exists.' : undefined),
                onChangeAsyncDebounceMs: 1000
            }
        });
        field.mount();

        return { field, form };
    }

    it('SubmitWhenValidated_EmailStillValidating_SubmitsOnceValidationSettles', async () => {
        // Arrange
        vi.useFakeTimers();
        const onSubmit = vi.fn();
        const { field, form } = createEmailForm(onSubmit);
        field.handleChange('new@example.test');
        await vi.advanceTimersByTimeAsync(100);

        // Act
        const submitting = submitWhenValidated(form);
        await vi.advanceTimersByTimeAsync(1000);
        await submitting;

        // Assert
        expect(onSubmit).toHaveBeenCalledExactlyOnceWith('new@example.test');
    });

    it('SubmitWhenValidated_EmailStillValidating_PlainHandleSubmitDropsTheFirstAttempt', async () => {
        // Arrange
        vi.useFakeTimers();
        const onSubmit = vi.fn();
        const { field, form } = createEmailForm(onSubmit);
        field.handleChange('new@example.test');
        await vi.advanceTimersByTimeAsync(100);

        // Act
        await form.handleSubmit();
        await vi.advanceTimersByTimeAsync(1000);

        // Assert
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it('SubmitWhenValidated_EmailAlreadyExists_DoesNotSubmit', async () => {
        // Arrange
        vi.useFakeTimers();
        const onSubmit = vi.fn();
        const { field, form } = createEmailForm(onSubmit);
        field.handleChange('taken@example.test');
        await vi.advanceTimersByTimeAsync(100);

        // Act
        const submitting = submitWhenValidated(form);
        await vi.advanceTimersByTimeAsync(1000);
        await submitting;

        // Assert
        expect(onSubmit).not.toHaveBeenCalled();
        expect(field.state.meta.errors).toContain('Email already exists.');
    });

    it('SubmitWhenValidated_NothingValidating_SubmitsImmediately', async () => {
        // Arrange
        const onSubmit = vi.fn();
        const { form } = createEmailForm(onSubmit);

        // Act
        await submitWhenValidated(form);

        // Assert
        expect(onSubmit).toHaveBeenCalledOnce();
    });
});
