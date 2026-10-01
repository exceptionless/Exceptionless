export function choose(flag: boolean) {
    if (flag) return 'left';
    return 'right';
}

export function parse(value: string) {
    const parsed = JSON.parse(value);
    return parsed;
}
