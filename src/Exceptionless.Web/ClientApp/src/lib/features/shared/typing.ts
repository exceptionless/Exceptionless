export function isJSONString(value: unknown): value is string {
    if (!isString(value)) {
        return false;
    }

    try {
        JSON.parse(value);
        return true;
    } catch {
        return false;
    }
}

export function isObject(value: unknown): value is Record<string, unknown> {
    return Object.prototype.toString.call(value) === '[object Object]';
}

export function isString(value: unknown): value is string {
    return Object.prototype.toString.call(value) === '[object String]';
}

export function isXmlString(value: unknown): value is string {
    if (!isString(value)) {
        return false;
    }

    const trimmedValue = value.trim();
    return trimmedValue.startsWith('<') && trimmedValue.endsWith('>');
}
