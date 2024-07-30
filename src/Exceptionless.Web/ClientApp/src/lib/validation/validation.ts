import { ProblemDetails } from '@exceptionless/fetchclient';
import { validate as classValidate } from 'class-validator';

export async function validate(data: null | object): Promise<null | ProblemDetails> {
    if (data === null) return null;

    const validationErrors = await classValidate(data);

    if (validationErrors.length === 0) return null;

    const problem = new ProblemDetails();
    for (const ve of validationErrors) {
        problem.errors[ve.property] = Object.values(ve.constraints || {}).map((message) => {
            return `${message.charAt(0).toUpperCase()}${message.slice(1)}.`;
        });
    }

    return problem;
}
