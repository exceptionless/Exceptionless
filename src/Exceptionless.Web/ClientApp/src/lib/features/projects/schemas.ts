import {
    UpdateProjectSchema as GeneratedUpdateProjectSchema,
    type NewProjectFormData,
    NewProjectSchema,
    type NotificationSettingsFormData,
    NotificationSettingsSchema
} from '$generated/schemas';
import { discriminatedUnion, type infer as Infer, literal, nullable, number, object, optional, string, enum as zodEnum } from 'zod';

export { type NewProjectFormData, NewProjectSchema, type NotificationSettingsFormData, NotificationSettingsSchema };

export const ClientConfigurationSettingSchema = object({
    key: string().min(1, 'Key is required'),
    value: string().min(1, 'Value is required')
});
export type ClientConfigurationSettingFormData = Infer<typeof ClientConfigurationSettingSchema>;

export const FixedIngestLimitSchema = object({
    fixed_limit: number().int().min(1, 'Limit must be at least 1'),
    percent_of_organization_limit: optional(nullable(number())),
    type: literal(0)
});

export const PercentIngestLimitSchema = object({
    fixed_limit: optional(nullable(number())),
    percent_of_organization_limit: number().positive('Percentage must be greater than 0').max(100, 'Percentage must be at most 100'),
    type: literal(1)
});

export const IngestLimitSchema = discriminatedUnion('type', [FixedIngestLimitSchema, PercentIngestLimitSchema]);
export type IngestLimitFormData = Infer<typeof IngestLimitSchema>;

export const UpdateProjectSchema = GeneratedUpdateProjectSchema.partial();
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

export const UpdateProjectIngestLimitSchema = object({
    ingest_limit: IngestLimitSchema.nullable().optional()
});
export type UpdateProjectIngestLimitFormData = Infer<typeof UpdateProjectIngestLimitSchema>;

export const ProjectBudgetCardSchema = object({
    type: zodEnum(['none', 'fixed', 'percent']),
    value: string()
}).superRefine((budget, context) => {
    if (budget.type === 'none') {
        return;
    }

    const value = budget.value.trim();
    if (budget.type === 'fixed') {
        const numericValue = Number(value);
        if (!/^\d+$/.test(value) || !Number.isSafeInteger(numericValue) || numericValue < 1 || numericValue > 2_147_483_647) {
            context.addIssue({ code: 'custom', message: 'Enter a whole number from 1 to 2,147,483,647.', path: ['value'] });
        }

        return;
    }

    const numericValue = Number(value);
    if (!/^(?:\d+\.?\d*|\.\d+)$/.test(value) || !Number.isFinite(numericValue) || numericValue <= 0 || numericValue > 100) {
        context.addIssue({ code: 'custom', message: 'Enter a percentage greater than 0 and no more than 100.', path: ['value'] });
    }
});
export type ProjectBudgetCardFormData = Infer<typeof ProjectBudgetCardSchema>;
