// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces

declare global {
    namespace App {
        interface Error {
            /** Exceptionless reference ID for errors reported by the client error hook. */
            errorId?: string;
        }
        // interface Locals {}
        // interface PageData {}
        // interface PageState {}
        // interface Platform {}
    }
}

export {};
