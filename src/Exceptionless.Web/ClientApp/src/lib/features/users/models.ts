import { IsEmail, IsOptional } from 'class-validator';

export { UpdateEmailAddressResult, User, ViewUser } from '$generated/api';

export class InviteUserForm {
    @IsEmail({ require_tld: false }, { message: 'Please enter a valid email address.' })
    email!: string;
}

export class UpdateUser {
    @IsOptional() email_notifications_enabled?: boolean;
    @IsOptional() full_name?: string;
}

export class UpdateUserEmailAddress {
    @IsEmail({ require_tld: false }, { message: 'Email must be a valid email address.' })
    email_address!: string;
}
