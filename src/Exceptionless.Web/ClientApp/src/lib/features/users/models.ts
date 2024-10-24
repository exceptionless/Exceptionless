import { IsOptional } from 'class-validator';

export { UpdateEmailAddressResult, User } from '$generated/api';

export class UpdateUser {
    @IsOptional() email_notifications_enabled?: boolean;
    @IsOptional() full_name?: string;
}
