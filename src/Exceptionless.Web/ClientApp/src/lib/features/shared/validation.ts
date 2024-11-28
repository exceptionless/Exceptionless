import { ProblemDetails } from '@exceptionless/fetchclient';
import { validate as classValidate } from 'class-validator';
import { type ErrorStatus, type FormPathLeavesWithErrors, setError, setMessage, type SuperValidated } from 'sveltekit-superforms';

export function applyServerSideErrors<T extends Record<string, unknown> = Record<string, unknown>, M = unknown, In extends Record<string, unknown> = T>(
    form: SuperValidated<T, M, In>,
    problem: null | ProblemDetails
) {
    if (!problem || problem.status !== 422) {
        setMessage(form, problem?.title as M, { status: (problem?.status as ErrorStatus) ?? 500 });
        return;
    }

    for (const key in problem.errors) {
        const errors = problem.errors[key] as string[];
        setError(form, key as FormPathLeavesWithErrors<T>, errors, { status: (problem?.status as ErrorStatus) ?? 500 });
    }
}

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
