export function match(param: string): param is 'discarded' | 'ignored' | 'mark-fixed' {
    return param === 'mark-fixed' || param === 'ignored' || param === 'discarded';
}
