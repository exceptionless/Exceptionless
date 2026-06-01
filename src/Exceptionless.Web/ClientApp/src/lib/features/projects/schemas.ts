import {
    UpdateProjectSchema as GeneratedUpdateProjectSchema,
    type NewProjectFormData,
    NewProjectSchema,
    type NotificationSettingsFormData,
    NotificationSettingsSchema
} from '$generated/schemas';
import { discriminatedUnion, literal, type infer as Infer, nullable, number, object, optional, string } from 'zod';

export { type NewProjectFormData, NewProjectSchema, type NotificationSettingsFormData, NotificationSettingsSchema };

export const ClientConfigurationSettingSchema = object({
    key: string().min(1, 'Key is required'),
    value: string().min(1, 'Value is required')
});
export type ClientConfigurationSettingFormData = Infer<typeof ClientConfigurationSettingSchema>;

export const FixedIngestLimitSchema = object({
    type: literal(0),
    fixed_limit: number().int().min(1, 'Limit must be at least 1').nullable().optional(),
    percent_of_organization_limit: optional(nullable(number()))
});

export const PercentIngestLimitSchema = object({
    type: literal(1),
    fixed_limit: optional(nullable(number())),
    percent_of_organization_limit: number().min(1, 'Percentage must be at least 1').max(999, 'Percentage must be at most 999').nullable().optional()
});

export const IngestLimitSchema = discriminatedUnion('type', [FixedIngestLimitSchema, PercentIngestLimitSchema]);
export type IngestLimitFormData = Infer<typeof IngestLimitSchema>;

export const UpdateProjectSchema = GeneratedUpdateProjectSchema.partial();
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

export const UpdateProjectIngestLimitSchema = object({
    ingest_limit: IngestLimitSchema.nullable().optional()
});
export type UpdateProjectIngestLimitFormData = Infer<typeof UpdateProjectIngestLimitSchema>;
