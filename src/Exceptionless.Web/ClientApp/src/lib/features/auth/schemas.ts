import { dev } from '$app/environment';
import { ChangePasswordModelSchema, LoginSchema as GeneratedLoginSchema, ResetPasswordModelSchema } from '$generated/schemas';
import { email, type infer as Infer, object, string } from 'zod';

export { type SignupFormData, SignupSchema } from '$generated/schemas';

// In dev mode, allow addresses like test@localhost (no TLD required)
export const LoginSchema = dev
    ? GeneratedLoginSchema.extend({
          email: string().min(1, 'Email is required').regex(/^[^\s@]+@[^\s@]+$/, 'Please enter a valid email address')
      })
    : GeneratedLoginSchema;
export type LoginFormData = Infer<typeof GeneratedLoginSchema>;

export const ChangePasswordSchema = ChangePasswordModelSchema.extend({
    confirm_password: string().min(6, 'Confirm password must be at least 6 characters').max(100, 'Confirm password must be at most 100 characters'),
    current_password: string() // Allow empty for users without local account
}).refine((data) => data.password === data.confirm_password, {
    message: 'Passwords do not match',
    path: ['confirm_password']
});
export type ChangePasswordFormData = Infer<typeof ChangePasswordSchema>;

export const ChangePasswordWithCurrentSchema = ChangePasswordModelSchema.extend({
    confirm_password: string().min(6, 'Confirm password must be at least 6 characters').max(100, 'Confirm password must be at most 100 characters')
}).refine((data) => data.password === data.confirm_password, {
    message: 'Passwords do not match',
    path: ['confirm_password']
});
export type ChangePasswordWithCurrentFormData = Infer<typeof ChangePasswordWithCurrentSchema>;

export const ForgotPasswordSchema = object({
    email: email('Please enter a valid email address').min(1, 'Email is required')
});
export type ForgotPasswordFormData = Infer<typeof ForgotPasswordSchema>;

export const ResetPasswordSchema = ResetPasswordModelSchema.extend({
    confirm_password: string().min(6, 'Confirm password must be at least 6 characters').max(100, 'Confirm password must be at most 100 characters')
}).refine((data) => data.password === data.confirm_password, {
    message: 'Passwords do not match',
    path: ['confirm_password']
});
export type ResetPasswordFormData = Infer<typeof ResetPasswordSchema>;
