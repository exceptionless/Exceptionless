import { email, type infer as Infer, object } from 'zod';

export { type UpdateUserFormData, UpdateUserSchema } from '$generated/schemas';

export const InviteUserSchema = object({
    email: email('Please enter a valid email address')
});
export type InviteUserFormData = Infer<typeof InviteUserSchema>;

export const UpdateUserEmailAddressSchema = object({
    email_address: email('Email must be a valid email address')
});
export type UpdateUserEmailAddressFormData = Infer<typeof UpdateUserEmailAddressSchema>;
