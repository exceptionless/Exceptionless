import { ProblemDetails } from '@exceptionless/fetchclient';

export type FieldWithErrors = {
    state: {
        meta: {
            errors: unknown[];
        };
    };
};

/**
 * ARIA helper: returns `true` when invalid, otherwise `undefined` (omit attribute).
 */
export function ariaInvalid(field: FieldWithErrors): true | undefined {
    return field.state.meta.errors.length > 0 ? true : undefined;
}

/**
 * Extracts error message from various error formats.
 * Handles:
 * - String errors directly
 * - Standard Schema V1 Issue objects (e.g., from Zod): { message: string, path?: (string|number)[] }
 * - Generic objects with a message property
 * - Falls back to String(error) for unknown formats
 */
export function extractErrorMessage(error: unknown): string {
    if (typeof error === 'string') {
        return error;
    }
    if (error && typeof error === 'object' && 'message' in error && typeof (error as { message: unknown }).message === 'string') {
        return (error as { message: string }).message;
    }
    return String(error);
}

/**
 * Helper to get the first error message for a field.
 * Returns undefined if no errors exist.
 *
 * @example
 * ```svelte
 * {#if getFieldError(field)}
 *   <span class="error">{getFieldError(field)}</span>
 * {/if}
 * ```
 */
export function getFieldError(field: FieldWithErrors): string | undefined {
    const errors = field.state.meta.errors;
    if (errors.length === 0) {
        return undefined;
    }
    return extractErrorMessage(errors[0]);
}

/**
 * Helper to get all error messages for a field joined by a separator.
 *
 * @example
 * ```svelte
 * {#if getFieldErrors(field)}
 *   <span class="error">{getFieldErrors(field)}</span>
 * {/if}
 * ```
 */
export function getFieldErrors(field: FieldWithErrors, separator = ', '): string | undefined {
    const errors = field.state.meta.errors;
    if (errors.length === 0) {
        return undefined;
    }
    return errors.map((e) => extractErrorMessage(e)).join(separator);
}

/**
 * Extracts all form-level error messages from form submission errors.
 * Returns a summary of all errors as a string or array of strings.
 * Use with form.Subscribe selector={(state) => state.errors}
 *
 * @example
 * ```svelte
 * <form.Subscribe selector={(state) => state.errors}>
 *   {#snippet children(errors)}
 *     <ErrorMessage message={getFormError(errors)}></ErrorMessage>
 *   {/snippet}
 * </form.Subscribe>
 * ```
 */
export function getFormErrorMessages(errors?: unknown[]): string | string[] | undefined {
    if (!errors || errors.length === 0) {
        return undefined;
    }

    const messages: string[] = [];
    for (const error of errors) {
        if (typeof error === 'string') {
            messages.push(error);
        } else if (typeof error === 'object' && error !== null && 'form' in error) {
            const formError = (error as { form?: string }).form;
            if (typeof formError === 'string') {
                messages.push(formError);
            }
        }
    }

    return messages;
}

/**
 * Helper to check if a field has validation errors.
 * Useful for conditional styling of form fields.
 *
 * @example
 * ```svelte
 * <input class:error={hasFieldError(field)} />
 * ```
 */
export function hasFieldError(field: FieldWithErrors): boolean {
    return field.state.meta.errors.length > 0;
}

/**
 * Type guard to check if an error is a ProblemDetails instance.
 */
export function isProblemDetails(error: unknown): error is ProblemDetails {
    return error instanceof ProblemDetails;
}

/**
 * Maps field errors to the format expected by Field.Error component.
 * Use this with TanStack Form's field.state.meta.errors.
 *
 * @example
 * ```svelte
 * <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
 * ```
 */
export function mapFieldErrors(errors: unknown[]): { message: string }[] {
    return errors.map((e) => ({ message: extractErrorMessage(e) }));
}

/**
 * Converts ProblemDetails errors to TanStack Form field errors format.
 * Use this in onSubmitAsync validators to return server-side validation errors.
 *
 * @example
 * ```ts
 * const form = createForm(() => ({
 *   defaultValues: { email: '', password: '' },
 *   validators: {
 *     onSubmit: zodValidator(loginSchema)
 *   },
 *   onSubmitAsync: async ({ value }) => {
 *     try {
 *       await loginUser(value);
 *       return null;
 *     } catch (error) {
 *       if (error instanceof ProblemDetails) {
 *         return problemDetailsToFormErrors(error);
 *       }
 *       throw error;
 *     }
 *   }
 * }));
 * ```
 */
export function problemDetailsToFormErrors(problem: null | ProblemDetails): null | {
    fields?: Record<string, string>;
    form?: string;
} {
    if (!problem) {
        return null;
    }

    const result: { fields?: Record<string, string>; form?: string } = {};

    // Handle general/form-level errors
    const generalErrors = problem.errors?.['general']?.join(', ');
    if (generalErrors) {
        result.form = generalErrors;
    } else if (problem.title && problem.status !== 422) {
        result.form = problem.title;
    }

    // Handle field-level errors (422 validation errors)
    if (problem.status === 422 && problem.errors) {
        result.fields = {};
        for (const key in problem.errors) {
            if (key === 'general') {
                continue;
            }

            const errors = problem.errors[key] as string[];
            // TODO: Convert snake_case field names to match form field names??
            result.fields[key] = errors.join(', ');
        }
    }

    return Object.keys(result).length > 0 ? result : null;
}
