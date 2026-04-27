import type { NewSavedView as GeneratedNewSavedView, UpdateSavedView, ViewSavedView } from '$generated/api';

// NewSavedView.is_default is generated as required boolean, but the frontend
// omits it (defaults to false on the server), so we make it optional here.
export type NewSavedView = Omit<GeneratedNewSavedView, 'is_default'> & {
    is_default?: boolean;
};

export type SavedView = ViewSavedView;

export type { UpdateSavedView };
