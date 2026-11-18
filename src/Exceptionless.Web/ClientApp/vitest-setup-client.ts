import '@testing-library/jest-dom/vitest';
import { beforeEach, vi } from 'vitest';

// jsdom does not implement scrolling used by Bits UI command menus.
Element.prototype.scrollIntoView = vi.fn();

// required for svelte5 + jsdom as jsdom does not support matchMedia
Object.defineProperty(window, 'matchMedia', {
    enumerable: true,
    value: vi.fn().mockImplementation((query) => ({
        addEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
        matches: false,
        media: query,
        onchange: null,
        removeEventListener: vi.fn()
    })),
    writable: true
});

// Mock localStorage for all client tests
let storage: Record<string, string> = {};

beforeEach(() => {
    storage = {};
});

const mockStorage = {
    clear: () => {
        storage = {};
    },
    getItem: (key: string) => storage[key] ?? null,
    key: (index: number) => Object.keys(storage)[index] ?? null,
    get length() {
        return Object.keys(storage).length;
    },
    removeItem: (key: string) => {
        delete storage[key];
    },
    setItem: (key: string, value: string) => {
        storage[key] = value;
    }
};

Object.defineProperty(window, 'localStorage', {
    configurable: true,
    value: mockStorage
});

// add more mocks here if you need them

// SvelteKit's dev server provides public environment variables to the page; unit tests
// start without one, so every declared variable is unset unless a test mocks it.
vi.mock('$app/env/public', async () => {
    const { variables } = await import('./src/env');
    return Object.fromEntries(Object.keys(variables).map((name) => [name, undefined]));
});
