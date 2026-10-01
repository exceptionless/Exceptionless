export const coverageInclude = ['src/**/*.{js,ts,svelte}'];
export const coverageExclude = [
    '**/*.{test,spec,stories}.{js,ts,svelte}',
    '**/*-harness.svelte',
    '**/*.d.ts',
    '**/__tests__/**',
    '**/__mocks__/**',
    'src/lib/generated/**'
];

export function browserSource(path: string): boolean {
    return eligibleSource(path) && !path.includes('/server/') && !/(\.server|\+server)\.[jt]s$/.test(path);
}

export function eligibleSource(path: string): boolean {
    return (
        path.startsWith('src/') &&
        /\.(js|ts|svelte)$/.test(path) &&
        !/\.(test|spec|stories)\.(js|ts|svelte)$/.test(path) &&
        !/-harness\.svelte$/.test(path) &&
        !path.endsWith('.d.ts') &&
        !path.includes('/__tests__/') &&
        !path.includes('/__mocks__/') &&
        !path.startsWith('src/lib/generated/')
    );
}
