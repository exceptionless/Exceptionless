import type { NewSavedView as GeneratedNewSavedView, UpdateSavedView as GeneratedUpdateSavedView, ViewSavedView } from '$generated/api';

// TODO: Remove these overrides after regenerating API types (`npm run generate-models`).
// The generated types are stale: NewSavedView.filter is required (should be optional),
// NewSavedView.is_default is required (should be optional), and
// UpdateSavedView.columns is unknown[] (should be Record<string, boolean>).
export type NewSavedView = Omit<GeneratedNewSavedView, 'filter' | 'is_default'> & {
    filter?: null | string;
    is_default?: boolean;
};

export type SavedView = ViewSavedView;

export type UpdateSavedView = Omit<GeneratedUpdateSavedView, 'columns' | 'is_default'> & {
    columns?: Record<string, boolean>;
    is_default?: boolean;
};
