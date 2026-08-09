import { type infer as Infer, object, string, enum as zodEnum } from 'zod';

import { INDEX_TYPES } from './models';

export const CustomFieldNameSchema = string()
    .trim()
    .min(1, 'Field name is required.')
    .max(100, 'Field name cannot exceed 100 characters.')
    .regex(/^[a-zA-Z0-9_.-]+$/, 'Use only letters, digits, underscore, dot, and dash.')
    .refine((name) => !name.startsWith('@'), 'Field names cannot start with @.')
    .refine(
        (name) => !/^(?:bool|date|double|float|int|keyword|long|string)-\d+$/i.test(name) && !/^(?:session-r|sessionend-d|haserror-b)$/i.test(name),
        'This name is reserved for internal search storage.'
    );

export const CreateCustomFieldSchema = object({
    description: string().max(500, 'Description cannot exceed 500 characters.'),
    indexType: zodEnum(INDEX_TYPES),
    name: CustomFieldNameSchema
});

export const UpdateCustomFieldSchema = object({
    description: string().max(500, 'Description cannot exceed 500 characters.')
});

export const QuickCreateCustomFieldSchema = object({
    indexType: zodEnum(INDEX_TYPES)
});

export type CreateCustomFieldFormData = Infer<typeof CreateCustomFieldSchema>;
export type QuickCreateCustomFieldFormData = Infer<typeof QuickCreateCustomFieldSchema>;
export type UpdateCustomFieldFormData = Infer<typeof UpdateCustomFieldSchema>;
