import { IsEmail, IsOptional } from 'class-validator';

export { UpdateEmailAddressResult, User } from '$generated/api';

export class UpdateUser {
    @IsOptional() email_notifications_enabled?: boolean;
    @IsOptional() full_name?: string;
}

export class UpdateUserEmailAddress {
    @IsEmail({ require_tld: false }, { message: 'Email must be a valid email address.' })
    email_address!: string;
}
