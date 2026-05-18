import type { APIRequestContext, APIResponse } from '@playwright/test';

import type { E2EEnvironment } from './environment';

export interface E2ECurrentUser {
    email_address?: string;
    id: string;
}

export interface E2EEvent {
    id: string;
    message?: string;
    reference_id?: string;
    stack_id?: string;
    type?: string;
}

export interface E2EOrganization {
    id: string;
    name: string;
}

export interface E2EProject {
    id: string;
    name: string;
    organization_id?: string;
}

export interface E2EStack {
    id: string;
    status?: string;
    title?: string;
}

export interface E2EToken {
    id: string;
    notes?: string;
}

interface TokenResult {
    token: string;
}

export class E2EApiClient {
    constructor(
        private readonly request: APIRequestContext,
        readonly environment: E2EEnvironment
    ) {}

    async createOrganization(token: string, name: string): Promise<E2EOrganization> {
        const response = await this.request.post(this.url('organizations'), {
            data: { name },
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [201], 'create organization');
        return toOrganization(await readJson(response));
    }

    async createProject(token: string, organizationId: string, name: string): Promise<E2EProject> {
        const response = await this.request.post(this.url('projects'), {
            data: {
                delete_bot_data_enabled: true,
                name,
                organization_id: organizationId
            },
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [201], 'create project');
        return toProject(await readJson(response));
    }

    async deleteCurrentUser(token: string): Promise<number> {
        const response = await this.request.delete(this.url('users/me'), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [202, 404], 'delete current user');
        return response.status();
    }

    async deleteOrganization(token: string, organizationId: string): Promise<number> {
        const response = await this.request.delete(this.url(`organizations/${organizationId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [202, 404], 'delete organization');
        return response.status();
    }

    async deleteProject(token: string, projectId: string): Promise<number> {
        const response = await this.request.delete(this.url(`projects/${projectId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [202, 404], 'delete project');
        return response.status();
    }

    async getAbout(): Promise<Record<string, unknown>> {
        const response = await this.request.get(this.url('about'));

        await expectStatus(response, [200], 'get about');
        return toRecord(await readJson(response), 'about response');
    }

    async getCurrentUser(token: string): Promise<E2ECurrentUser | undefined> {
        const response = await this.request.get(this.url('users/me'), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200, 401, 404], 'get current user');

        if (response.status() !== 200) {
            return undefined;
        }

        return toCurrentUser(await readJson(response));
    }

    async getEvent(token: string, eventId: string): Promise<E2EEvent> {
        const response = await this.request.get(this.url(`events/${eventId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200], 'get event');
        return toEvent(await readJson(response));
    }

    async getEventsByReference(token: string, projectId: string, referenceId: string): Promise<E2EEvent[]> {
        const response = await this.request.get(this.url(`projects/${projectId}/events/by-ref/${encodeURIComponent(referenceId)}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200, 404], 'get events by reference');

        if (response.status() === 404) {
            return [];
        }

        return toEventArray(await readJson(response));
    }

    async getOrganization(token: string, organizationId: string): Promise<E2EOrganization | undefined> {
        const response = await this.request.get(this.url(`organizations/${organizationId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200, 404], 'get organization');

        if (response.status() === 404) {
            return undefined;
        }

        return toOrganization(await readJson(response));
    }

    async getOrganizations(token: string): Promise<E2EOrganization[]> {
        const response = await this.request.get(this.url('organizations'), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200], 'get organizations');
        return toOrganizationArray(await readJson(response));
    }

    async getProject(token: string, projectId: string): Promise<E2EProject | undefined> {
        const response = await this.request.get(this.url(`projects/${projectId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200, 404], 'get project');

        if (response.status() === 404) {
            return undefined;
        }

        return toProject(await readJson(response));
    }

    async getProjectDefaultToken(token: string, projectId: string): Promise<E2EToken> {
        const response = await this.request.get(this.url(`projects/${projectId}/tokens/default`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200, 201], 'get default project token');
        return toToken(await readJson(response));
    }

    async getStack(token: string, stackId: string): Promise<E2EStack> {
        const response = await this.request.get(this.url(`stacks/${stackId}`), {
            headers: this.authHeaders(token)
        });

        await expectStatus(response, [200], 'get stack');
        return toStack(await readJson(response));
    }

    async login(): Promise<string> {
        if (!this.environment.email || !this.environment.password) {
            throw new Error('E2E_EMAIL and E2E_PASSWORD are required when using API login.');
        }

        const response = await this.request.post(this.url('auth/login'), {
            data: {
                email: this.environment.email,
                password: this.environment.password
            }
        });

        await expectStatus(response, [200], 'login');
        const result = toTokenResult(await readJson(response));

        return result.token;
    }

    async pollForEventByReference(token: string, projectId: string, referenceId: string, timeoutMs = 90_000): Promise<E2EEvent> {
        const deadline = Date.now() + timeoutMs;

        while (Date.now() < deadline) {
            const events = await this.getEventsByReference(token, projectId, referenceId);
            const event = events.find((item) => item.reference_id === referenceId) ?? events[0];

            if (event?.id) {
                return event;
            }

            await delay(2_000);
        }

        throw new Error(`Timed out waiting for E2E event with reference id ${referenceId}`);
    }

    async signup(name: string, email: string, password: string): Promise<string> {
        const response = await this.request.post(this.url('auth/signup'), {
            data: {
                email,
                name,
                password
            }
        });

        await expectStatus(response, [200], 'signup');
        const result = toTokenResult(await readJson(response));

        return result.token;
    }

    async submitEvent(projectId: string, projectToken: string, event: Record<string, unknown>): Promise<void> {
        const response = await this.request.post(this.url(`projects/${projectId}/events?access_token=${encodeURIComponent(projectToken)}`), {
            data: event
        });

        await expectStatus(response, [202], 'submit event');
    }

    async waitForCurrentUserDeleted(token: string, timeoutMs = 30_000): Promise<void> {
        const deadline = Date.now() + timeoutMs;
        let lastError: Error | undefined;

        while (Date.now() < deadline) {
            try {
                const user = await this.getCurrentUser(token);
                if (!user) {
                    return;
                }
            } catch (error) {
                lastError = error instanceof Error ? error : new Error(String(error));
            }

            await delay(1_000);
        }

        throw new Error(`Timed out waiting for generated E2E user to be inaccessible after deletion${lastError ? `: ${lastError.message}` : ''}`);
    }

    async waitForOrganizationDeleted(token: string, organizationId: string, timeoutMs = 30_000): Promise<void> {
        const deadline = Date.now() + timeoutMs;
        let lastError: Error | undefined;

        while (Date.now() < deadline) {
            try {
                const organization = await this.getOrganization(token, organizationId);
                if (!organization) {
                    return;
                }
            } catch (error) {
                lastError = error instanceof Error ? error : new Error(String(error));
            }

            await delay(1_000);
        }

        throw new Error(
            `Timed out waiting for E2E organization ${organizationId} to be inaccessible after deletion${lastError ? `: ${lastError.message}` : ''}`
        );
    }

    async waitForProjectDeleted(token: string, projectId: string, timeoutMs = 30_000): Promise<void> {
        const deadline = Date.now() + timeoutMs;
        let lastError: Error | undefined;

        while (Date.now() < deadline) {
            try {
                const project = await this.getProject(token, projectId);
                if (!project) {
                    return;
                }
            } catch (error) {
                lastError = error instanceof Error ? error : new Error(String(error));
            }

            await delay(1_000);
        }

        throw new Error(`Timed out waiting for E2E project ${projectId} to be inaccessible after deletion${lastError ? `: ${lastError.message}` : ''}`);
    }

    private authHeaders(token: string): Record<string, string> {
        return {
            Authorization: `Bearer ${token}`
        };
    }

    private url(path: string): string {
        const normalizedPath = path.replace(/^\/+/, '');
        return `${this.environment.apiUrl}/${normalizedPath}`;
    }
}

async function delay(ms: number): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, ms));
}

async function expectStatus(response: APIResponse, expectedStatuses: number[], operation: string): Promise<void> {
    if (expectedStatuses.includes(response.status())) {
        return;
    }

    const body = await response.text();
    throw new Error(`${operation} failed with status ${response.status()} ${response.statusText()}${body ? `: ${body}` : ''}`);
}

function getOptionalString(value: Record<string, unknown>, key: string): string | undefined {
    const property = value[key];
    return typeof property === 'string' ? property : undefined;
}

function getRequiredString(value: Record<string, unknown>, key: string, context: string): string {
    const property = getOptionalString(value, key);

    if (!property) {
        throw new Error(`${context} did not contain a string ${key} value`);
    }

    return property;
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

async function readJson(response: APIResponse): Promise<unknown> {
    const text = await response.text();

    if (!text) {
        return undefined;
    }

    return JSON.parse(text) as unknown;
}

function toCurrentUser(value: unknown): E2ECurrentUser {
    const record = toRecord(value, 'current user response');

    return {
        email_address: getOptionalString(record, 'email_address'),
        id: getRequiredString(record, 'id', 'current user response')
    };
}

function toEvent(value: unknown): E2EEvent {
    const record = toRecord(value, 'event response');

    return {
        id: getRequiredString(record, 'id', 'event response'),
        message: getOptionalString(record, 'message'),
        reference_id: getOptionalString(record, 'reference_id'),
        stack_id: getOptionalString(record, 'stack_id'),
        type: getOptionalString(record, 'type')
    };
}

function toEventArray(value: unknown): E2EEvent[] {
    if (!Array.isArray(value)) {
        throw new Error('events by reference response was not an array');
    }

    return value.map(toEvent);
}

function toOrganization(value: unknown): E2EOrganization {
    const record = toRecord(value, 'organization response');

    return {
        id: getRequiredString(record, 'id', 'organization response'),
        name: getRequiredString(record, 'name', 'organization response')
    };
}

function toOrganizationArray(value: unknown): E2EOrganization[] {
    if (!Array.isArray(value)) {
        throw new Error('organizations response was not an array');
    }

    return value.map(toOrganization);
}

function toProject(value: unknown): E2EProject {
    const record = toRecord(value, 'project response');

    return {
        id: getRequiredString(record, 'id', 'project response'),
        name: getRequiredString(record, 'name', 'project response'),
        organization_id: getOptionalString(record, 'organization_id')
    };
}

function toRecord(value: unknown, context: string): Record<string, unknown> {
    if (!isRecord(value)) {
        throw new Error(`${context} was not an object`);
    }

    return value;
}

function toStack(value: unknown): E2EStack {
    const record = toRecord(value, 'stack response');

    return {
        id: getRequiredString(record, 'id', 'stack response'),
        status: getOptionalString(record, 'status'),
        title: getOptionalString(record, 'title')
    };
}

function toToken(value: unknown): E2EToken {
    const record = toRecord(value, 'token response');

    return {
        id: getRequiredString(record, 'id', 'token response'),
        notes: getOptionalString(record, 'notes')
    };
}

function toTokenResult(value: unknown): TokenResult {
    const record = toRecord(value, 'token result');

    return {
        token: getRequiredString(record, 'token', 'token result')
    };
}
