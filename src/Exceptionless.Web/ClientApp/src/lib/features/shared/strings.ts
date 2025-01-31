export function getInitials(value: string | undefined, maxLength: number = 2, defaultValue: string = 'NA'): string {
    if (!value) {
        return defaultValue;
    }

    const trimmedValue = value.trim();
    const initials = trimmedValue
        .split(' ')
        .map((v) => v.trim())
        .filter((v) => v.length > 0)
        .map((v) => v[0]?.toLocaleUpperCase())
        .join('');

    if (initials.length === 1 && trimmedValue.length > 1) {
        return trimmedValue.substring(0, maxLength).toLocaleUpperCase();
    }

    return initials.length > maxLength ? initials.substring(0, maxLength) : initials;
}
