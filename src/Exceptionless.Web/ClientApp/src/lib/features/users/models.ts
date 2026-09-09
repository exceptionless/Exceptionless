import type { ViewCurrentUser as GeneratedViewCurrentUser, ProductTourState } from '$generated/api';

export type { ViewOAuthGrant as OAuthGrant, ProductTourState, UpdateEmailAddressResult, ViewUser } from '$generated/api';

export interface InviteUserForm {
    email: string;
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
