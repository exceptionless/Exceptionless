const DIAGNOSTIC_PROPERTY = 'svelte_effect_depth';
const MAX_APPENDED_STACK_FRAMES = 60;
const MAX_EFFECT_CHAIN_LENGTH = 12;
const MAX_STACK_CAPTURES_PER_SOURCE = 12;
const MAX_STACK_FRAMES = 12;
const MAX_STACKS_PER_SOURCE = 4;
const MAX_STATE_SOURCES = 8;
const STATE_UPDATE_CAPTURE_THRESHOLD = 5;

interface EffectDepthDiagnostics {
    activeElement: null | {
        dataSlot?: string;
        role?: string;
        tagName: string;
        type?: string;
    };
    effectChain: { flags?: number; functionName?: string }[];
    lastInteraction: null | {
        target: EffectDepthDiagnostics['activeElement'];
        timestamp: string;
        type: string;
    };
    route: string;
    stateSources: {
        count: number;
        stacks: { count: number; stack: string }[];
    }[];
    totalStateUpdates: number;
    version: 1;
    visibility: DocumentVisibilityState;
}

interface StateSourceUpdates {
    captureCount: number;
    count: number;
    stacks: Map<string, number>;
}

interface SvelteEffectDepthRuntimeDiagnostics {
    attachEffectDepthError: (error: unknown, effect: unknown) => void;
    endFlush: () => void;
    recordStateUpdate: (source: unknown) => void;
    startFlush: () => void;
}

declare global {
    var __exceptionlessSvelteEffectDepthDiagnostics: SvelteEffectDepthRuntimeDiagnostics | undefined;
}

let stateSourceUpdates: Map<object, StateSourceUpdates> | undefined;
let totalStateUpdates = 0;
let lastInteraction: EffectDepthDiagnostics['lastInteraction'] = null;

export function installSvelteEffectDepthDiagnostics() {
    if (globalThis.__exceptionlessSvelteEffectDepthDiagnostics) {
        return;
    }

    Object.defineProperty(globalThis, '__exceptionlessSvelteEffectDepthDiagnostics', {
        configurable: true,
        value: {
            attachEffectDepthError,
            endFlush,
            recordStateUpdate,
            startFlush
        }
    });

    for (const type of ['change', 'click', 'keydown', 'pointerdown', 'pointerup']) {
        globalThis.addEventListener?.(type, recordInteraction, { capture: true, passive: true });
    }
}

function attachEffectDepthError(error: unknown, effect: unknown) {
    if (!(error instanceof Error) || !stateSourceUpdates) {
        return;
    }

    try {
        const diagnostics = createDiagnostics(effect);
        Object.defineProperty(error, DIAGNOSTIC_PROPERTY, {
            configurable: true,
            enumerable: true,
            value: diagnostics
        });

        const updateFrames = diagnostics.stateSources
            .flatMap((source) => source.stacks.flatMap((entry) => getStackFrames(entry.stack)))
            .slice(0, MAX_APPENDED_STACK_FRAMES);
        if (updateFrames.length > 0) {
            error.stack = [error.stack ?? `${error.name}: ${error.message}`, 'Reactive state update call sites:', ...new Set(updateFrames)].join('\n');
        }
    } catch {
        // Diagnostics must never replace the original Svelte error.
    }
}

function createDiagnostics(effect: unknown): EffectDepthDiagnostics {
    const stateSources = [...(stateSourceUpdates?.values() ?? [])]
        .sort((left, right) => right.count - left.count)
        .slice(0, MAX_STATE_SOURCES)
        .map((source) => ({
            count: source.count,
            stacks: [...source.stacks.entries()].sort((left, right) => right[1] - left[1]).map(([stack, count]) => ({ count, stack }))
        }));

    return {
        activeElement: getActiveElement(),
        effectChain: getEffectChain(effect),
        lastInteraction,
        route: globalThis.location?.pathname ?? '',
        stateSources,
        totalStateUpdates,
        version: 1,
        visibility: globalThis.document?.visibilityState ?? 'hidden'
    };
}

function endFlush() {
    stateSourceUpdates = undefined;
    totalStateUpdates = 0;
}

function getActiveElement(): EffectDepthDiagnostics['activeElement'] {
    return getElementDetails(globalThis.document?.activeElement);
}

function getEffectChain(effect: unknown): EffectDepthDiagnostics['effectChain'] {
    const chain: EffectDepthDiagnostics['effectChain'] = [];
    let current = effect;

    while (isEffect(current) && chain.length < MAX_EFFECT_CHAIN_LENGTH) {
        chain.push({
            flags: typeof current.f === 'number' ? current.f : undefined,
            functionName: typeof current.fn === 'function' ? current.fn.name || undefined : undefined
        });
        current = current.parent;
    }

    return chain;
}

function getElementDetails(element: EventTarget | null | undefined): EffectDepthDiagnostics['activeElement'] {
    if (typeof HTMLElement === 'undefined' || !(element instanceof HTMLElement)) {
        return null;
    }

    return {
        dataSlot: element.dataset.slot,
        role: element.getAttribute('role') ?? undefined,
        tagName: element.tagName.toLowerCase(),
        type:
            (typeof HTMLInputElement !== 'undefined' && element instanceof HTMLInputElement) ||
            (typeof HTMLButtonElement !== 'undefined' && element instanceof HTMLButtonElement)
                ? element.type
                : undefined
    };
}

function getStackFrames(stack: string): string[] {
    return stack
        .split('\n')
        .filter((line) => /^\s*at\s/.test(line))
        .slice(0, MAX_STACK_FRAMES);
}

function isEffect(value: unknown): value is { f?: unknown; fn?: unknown; parent?: unknown } {
    return typeof value === 'object' && value !== null;
}

function recordInteraction(event: Event) {
    lastInteraction = {
        target: getElementDetails(event.target),
        timestamp: new Date().toISOString(),
        type: event.type
    };
}

function recordStateUpdate(source: unknown) {
    if (!stateSourceUpdates || (typeof source !== 'object' && typeof source !== 'function') || source === null) {
        return;
    }

    totalStateUpdates++;
    let updates = stateSourceUpdates.get(source);
    if (!updates) {
        updates = { captureCount: 0, count: 0, stacks: new Map() };
        stateSourceUpdates.set(source, updates);
    }

    updates.count++;
    if (
        updates.count <= STATE_UPDATE_CAPTURE_THRESHOLD ||
        updates.captureCount >= MAX_STACK_CAPTURES_PER_SOURCE ||
        updates.stacks.size >= MAX_STACKS_PER_SOURCE
    ) {
        return;
    }

    updates.captureCount++;
    const stack = new Error('Reactive state updated').stack;
    if (stack) {
        const frames = getStackFrames(stack).join('\n');
        if (frames) {
            updates.stacks.set(frames, (updates.stacks.get(frames) ?? 0) + 1);
        }
    }
}

function startFlush() {
    stateSourceUpdates = new Map();
    totalStateUpdates = 0;
}
