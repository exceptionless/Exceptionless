import type { PersistentEvent } from '$lib/models/api';
import type { ErrorInfo, InnerErrorInfo } from '$lib/models/client';
import { buildUrl } from './url';

export function getLocation(event: PersistentEvent) {
	const location = event.data?.['@location'];
	if (!location) {
		return null;
	}

	return [location.locality, location.level1, location.country]
		.filter((value) => value?.length)
		.reduce((a, b, index) => {
			a += (index > 0 ? ', ' : '') + b;
			return a;
		}, '');
}

export function getRequestInfoUrl(event: PersistentEvent) {
	const requestInfo = event.data?.['@request'];
	if (requestInfo) {
		return buildUrl(
			requestInfo.is_secure,
			requestInfo.host,
			requestInfo.port,
			requestInfo.path,
			requestInfo.query_string
		);
	}

	return null;
}

export function getMessage(event: PersistentEvent) {
	const error = event.data?.['@error'];
	if (error) {
		return getTargetInfoMessage(error) || event.message;
	}

	return event.message;
}

export function getErrorType(event: PersistentEvent) {
	const error = event.data?.['@error'];
	if (error) {
		const type = getTargetInfoExceptionType(error);
		return type || error.type || 'Unknown';
	}

	const simpleError = event.data?.['@simple_error'];
	return simpleError?.type || 'Unknown';
}

export function getErrors(error: ErrorInfo) {
	const errors = [];
	let currentError: InnerErrorInfo | undefined = error;
	while (currentError) {
		errors.push(currentError);
		currentError = currentError.inner;
	}

	return errors;
}

export function getTargetInfo(error: ErrorInfo) {
	return error.data?.['@target'];
}

export function getTargetInfoExceptionType(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.ErrorType;
}

export function getTargetInfoMethod(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.Method;
}

export function getTargetInfoMessage(error: ErrorInfo) {
	const target = getTargetInfo(error);
	return target?.Message;
}
