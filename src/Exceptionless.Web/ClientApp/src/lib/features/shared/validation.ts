import { ProblemDetails } from '@exceptionless/fetchclient';
import { validate as classValidate } from 'class-validator';
import { type FormPathLeavesWithErrors, setError, type SuperValidated } from 'sveltekit-superforms';

export async function validate(data: null | object): Promise<null | ProblemDetails> {
    if (data === null) {
        return null;
    }

    const validationErrors = await classValidate(data);
    if (validationErrors.length === 0) {
        return null;
    }

    const problem = new ProblemDetails();
    for (const ve of validationErrors) {
        problem.errors[ve.property] = Object.values(ve.constraints || {}).map((message) => {
            return `${message.charAt(0).toUpperCase()}${message.slice(1)}.`;
        });
    }

    return problem;
}

export function applyServerSideErrors<T extends Record<string, unknown> = Record<string, unknown>, M = unknown, In extends Record<string, unknown> = T>(
    form: SuperValidated<T, M, In>,
    problem: null | ProblemDetails
) {
    if (!problem || problem.status !== 422) {
        setMessage(form, 'An error occurred. Please try again.' as M);
        return;
    }

    for (const key in problem.errors) {
        const errors = problem.errors[key] as string[];
        setError(form, key as FormPathLeavesWithErrors<T>, errors);
    }
}
