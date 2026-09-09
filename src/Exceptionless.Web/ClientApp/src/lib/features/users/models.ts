import type { ViewCurrentUser as GeneratedViewCurrentUser } from '$generated/api';

export type { ViewOAuthGrant as OAuthGrant, UpdateEmailAddressResult, ViewUser } from '$generated/api';

export interface InviteUserForm {
    email: string;
}

export interface ProductTourState {
    app_overview?: null | string;
    app_welcome?: null | string;
    event_investigate?: null | string;
    exie_announcement?: null | string;
    exie_overview?: null | string;
    project_configure?: null | string;
    saved_view_create?: null | string;
}

export interface UpdateUser {
    email_notifications_enabled?: boolean;
    full_name?: string;
}

export interface UpdateUserEmailAddress {
    email_address: string;
}

export type ViewCurrentUser = Omit<GeneratedViewCurrentUser, 'product_tours'> & {
    product_tours?: ProductTourState;
};
