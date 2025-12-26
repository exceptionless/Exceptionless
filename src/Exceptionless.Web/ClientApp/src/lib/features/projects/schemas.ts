import { type infer as Infer, object, string } from 'zod';

import {
    type NewProjectFormData,
    NewProjectSchema,
    type NotificationSettingsFormData,
    NotificationSettingsSchema,
    UpdateProjectSchema as GeneratedUpdateProjectSchema
} from '$generated/schemas';

export { type NewProjectFormData, NewProjectSchema, type NotificationSettingsFormData, NotificationSettingsSchema };

export const UpdateProjectSchema = GeneratedUpdateProjectSchema.partial();
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;
