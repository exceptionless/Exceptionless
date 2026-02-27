import type { NewSavedView as GeneratedNewSavedView, UpdateSavedView as GeneratedUpdateSavedView, ViewSavedView } from '$generated/api';

export type NewSavedView = Omit<GeneratedNewSavedView, 'filter' | 'is_default'> & {
    filter?: null | string;
    is_default?: boolean;
};

export type SavedView = ViewSavedView;

export type UpdateSavedView = Omit<GeneratedUpdateSavedView, 'columns' | 'is_default'> & {
    columns?: Record<string, boolean>;
    is_default?: boolean;
};
