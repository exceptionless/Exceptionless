import { type infer as Infer, object, string } from 'zod';

import {
    type NewProjectFormData,
    NewProjectSchema,
    type NotificationSettingsFormData,
    NotificationSettingsSchema,
    UpdateProjectSchema as GeneratedUpdateProjectSchema
} from '$generated/schemas';

export { type NewProjectFormData, NewProjectSchema, type NotificationSettingsFormData, NotificationSettingsSchema };

export const ClientConfigurationSettingSchema = object({
    key: string().min(1, 'Key is required'),
    value: string().min(1, 'Value is required')
});
export type ClientConfigurationSettingFormData = Infer<typeof ClientConfigurationSettingSchema>;

export const UpdateProjectSchema = GeneratedUpdateProjectSchema.partial();
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;
