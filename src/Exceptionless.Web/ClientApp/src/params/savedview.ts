export function match(param: string): boolean {
    return /^(?![a-fA-F0-9]{24}$)[a-z0-9]+(?:-[a-z0-9]+)*$/.test(param);
}
