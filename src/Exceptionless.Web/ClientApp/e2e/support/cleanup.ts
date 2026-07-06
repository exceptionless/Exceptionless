export async function runCleanupStep(errors: Error[], name: string, action: () => Promise<void>): Promise<void> {
    try {
        await action();
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        errors.push(new Error(`${name}: ${message}`));
    }
}

export function throwIfCleanupFailed(errors: Error[]): void {
    if (errors.length === 0) {
        return;
    }

    throw new Error(`E2E cleanup failed:\n${errors.map((error) => `- ${error.message}`).join('\n')}`);
}
