import type { ProductTourProgress as GeneratedProductTourProgress, ViewCurrentUser as GeneratedViewCurrentUser } from '$generated/api';

export type { ViewOAuthGrant as OAuthGrant, UpdateEmailAddressResult, ViewUser } from '$generated/api';

export interface InviteUserForm {
    email: string;
}

export type ProductTourProgress = GeneratedProductTourProgress;

export type ProductTourStatus = 'completed' | 'dismissed';

export interface UpdateProductTourProgress {
    status: ProductTourStatus;
    version: number;
}

export interface UpdateUser {
    email_notifications_enabled?: boolean;
    full_name?: string;
}

export interface UpdateUserEmailAddress {
    email_address: string;
}

export type ViewCurrentUser = GeneratedViewCurrentUser;
