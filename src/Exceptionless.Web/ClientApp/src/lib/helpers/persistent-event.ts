import type { PersistentEvent } from '$lib/models/api';
import type { ErrorInfo, SimpleErrorInfo } from '$lib/models/client-data';
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

export function getErrors<T extends SimpleErrorInfo | ErrorInfo>(error: T | undefined): T[] {
	const errors: T[] = [];
	let current: T | undefined = error;
	while (current) {
		errors.push(current);
		current = current?.inner as T | undefined;
	}

	return errors;
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
