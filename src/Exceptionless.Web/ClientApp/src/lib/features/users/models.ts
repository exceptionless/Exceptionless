export { ProductTourStatus } from '$generated/api';
export type {
    ViewOAuthGrant as OAuthGrant,
    ProductTourProgress,
    UpdateEmailAddressResult,
    UpdateProductTourProgress,
    ViewCurrentUser,
    ViewUser
} from '$generated/api';

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
