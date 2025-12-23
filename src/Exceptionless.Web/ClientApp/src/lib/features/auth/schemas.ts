import { z } from 'zod';

export const loginSchema = z.object({
    email: z.string().min(1, 'Email is required'),
    invite_token: z.string().length(40, 'Invalid invite token').nullish(),
    password: z.string().min(6, 'Password must be at least 6 characters').max(100, 'Password must be at most 100 characters')
});

export type LoginFormData = z.infer<typeof loginSchema>;
