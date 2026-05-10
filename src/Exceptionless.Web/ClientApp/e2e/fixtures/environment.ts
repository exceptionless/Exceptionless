const DEFAULT_APP_URL = 'https://web-ex.dev.localhost:7131';
const DEFAULT_EMAIL = 'admin@exceptionless.test';
const DEFAULT_PASSWORD = 'tester';

export interface E2EEnvironment {
    apiUrl: string;
    appUrl: string;
    email: string;
    isProduction: boolean;
    password: string;
    runId: string;
}

export function getE2EEnvironment(): E2EEnvironment {
    const isProduction = getOptionalEnv('E2E_ENV') === 'production';
    const appUrl = getOptionalEnv('E2E_APP_URL') ?? DEFAULT_APP_URL;
    const email = getOptionalEnv('E2E_EMAIL') ?? (isProduction ? undefined : DEFAULT_EMAIL);
    const password = getOptionalEnv('E2E_PASSWORD') ?? (isProduction ? undefined : DEFAULT_PASSWORD);
    const runId = getOptionalEnv('E2E_RUN_ID') ?? getDefaultRunId();

    const missing = [
        ['E2E_APP_URL', appUrl],
        ['E2E_EMAIL', email],
        ['E2E_PASSWORD', password]
    ]
        .filter(([, value]) => !value)
        .map(([name]) => name);

    if (isProduction && missing.length > 0) {
        throw new Error(`Production E2E tests require these environment variables: ${missing.join(', ')}`);
    }

    if (!email || !password) {
        throw new Error('E2E test credentials are required. Set E2E_EMAIL and E2E_PASSWORD.');
    }

    const normalizedAppUrl = normalizeUrl(appUrl);

    return {
        apiUrl: getApiUrl(normalizedAppUrl),
        appUrl: normalizedAppUrl,
        email,
        isProduction,
        password,
        runId: sanitizeRunId(runId)
    };
}

function getApiUrl(appUrl: string): string {
    return new URL('/api/v2', `${appUrl}/`).toString().replace(/\/+$/, '');
}

function getDefaultRunId(): string {
    const githubRunId = getOptionalEnv('GITHUB_RUN_ID');
    const githubRunAttempt = getOptionalEnv('GITHUB_RUN_ATTEMPT');

    if (githubRunId) {
        return ['gh', githubRunId, githubRunAttempt].filter(Boolean).join('-');
    }

    return `local-${new Date().toISOString()}`;
}

function getOptionalEnv(name: string): string | undefined {
    const value = process.env[name]?.trim();
    return value ? value : undefined;
}

function normalizeUrl(url: string): string {
    return url.replace(/\/+$/, '');
}

function sanitizeRunId(value: string): string {
    return value
        .replace(/[^a-zA-Z0-9_-]/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 80);
}
