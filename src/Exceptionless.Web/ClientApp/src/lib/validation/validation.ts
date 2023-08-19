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

export async function getResponseValidationErrors<T extends object>(
	responseOrError: unknown,
	defaultMessage: string = 'An error occurred, please try again.'
): Promise<ValidationErrors<T>> {
	let message;

	if (responseOrError instanceof Error) {
		message = responseOrError.message;
	} else if (responseOrError instanceof Response) {
		switch (responseOrError.status) {
			case 400:
				message = responseOrError.statusText;
				break;
			case 422:
				// TODO: Problem details.
				break;
			default:
				message = await responseOrError.text();
				break;
		}
	}

	return { general: [message || defaultMessage] } as ValidationErrors<T>;
}
