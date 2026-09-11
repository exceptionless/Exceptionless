import type { ViewCurrentUser } from '$generated/api';

export type { ViewOAuthGrant as OAuthGrant, RecordProductTourResult, UpdateEmailAddressResult, ViewCurrentUser, ViewUser } from '$generated/api';

export interface InviteUserForm {
    email: string;
}

export type ProductTourState = NonNullable<ViewCurrentUser['product_tours']>;

export interface UpdateUser {
    email_notifications_enabled?: boolean;
    full_name?: string;
}

export interface UpdateUserEmailAddress {
    email_address: string;
}
