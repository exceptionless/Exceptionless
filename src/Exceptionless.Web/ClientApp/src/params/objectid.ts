export function match(param: string): boolean {
    return /^[a-fA-F0-9]{24}$/.test(param);
}
