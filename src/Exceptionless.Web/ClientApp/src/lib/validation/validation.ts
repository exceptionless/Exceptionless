import { validate as classValidate } from 'class-validator';

export type ValidationErrors<T> = Partial<Record<keyof T | 'general', string[]>>;

export async function validate<T extends object>(data: T): Promise<ValidationErrors<T>> {
	const result: ValidationErrors<T> = {};

	const validationErrors = await classValidate(data);
	if (validationErrors.length > 0) {
		for (const ve of validationErrors) {
			// TODO: Align client errors with server side error messages.
			result[ve.property as keyof T] = Object.values(ve.constraints || {}).map((message) => {
				return `${message.charAt(0).toUpperCase()}${message.slice(1)}.`;
			});
		}
	}

	return result;
}

export function getProblemDetailsValidationErrors<T extends object>(
	responseError: unknown,
	defaultMessage: string = 'Please try again'
): ValidationErrors<T> {
	if (responseError instanceof Error) {
		return { general: [responseError.message] } as ValidationErrors<T>;
	}

	return { general: [defaultMessage] } as ValidationErrors<T>;
}
