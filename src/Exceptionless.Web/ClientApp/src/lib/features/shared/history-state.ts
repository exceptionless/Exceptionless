export const detailSheetHistoryStateKey = '__exceptionlessDetailSheet';

export interface DetailSheetHistoryEntry {
    key: string;
    value: string;
}

export type DetailSheetPageState = App.PageState & { [detailSheetHistoryStateKey]?: DetailSheetHistoryEntry };

export function hasDetailSheetHistoryEntry(state: App.PageState): boolean {
    const entry = (state as DetailSheetPageState)[detailSheetHistoryStateKey];
    return typeof entry?.key === 'string' && typeof entry.value === 'string';
}

export function withoutDetailSheetHistoryEntry(state: App.PageState): App.PageState {
    const nextState = { ...state } as DetailSheetPageState;
    delete nextState[detailSheetHistoryStateKey];
    return nextState;
}
