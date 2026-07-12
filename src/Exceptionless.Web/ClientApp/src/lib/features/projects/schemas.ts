import {
    UpdateProjectSchema as GeneratedUpdateProjectSchema,
    type NewProjectFormData,
    NewProjectSchema,
    type NotificationSettingsFormData,
    NotificationSettingsSchema
} from '$generated/schemas';
import { type infer as Infer, object, string, enum as zodEnum } from 'zod';

export { type NewProjectFormData, NewProjectSchema, type NotificationSettingsFormData, NotificationSettingsSchema };

export const ClientConfigurationSettingSchema = object({
    key: string().min(1, 'Key is required'),
    value: string().min(1, 'Value is required')
});
export type ClientConfigurationSettingFormData = Infer<typeof ClientConfigurationSettingSchema>;

export const UpdateProjectSchema = GeneratedUpdateProjectSchema.partial();
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

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
    if (!/^(?:\d+(?:\.\d{0,4})?|\.\d{1,4})$/.test(value) || !Number.isFinite(numericValue) || numericValue <= 0 || numericValue > 100) {
        context.addIssue({ code: 'custom', message: 'Enter a percentage greater than 0 and no more than 100, with up to 4 decimal places.', path: ['value'] });
    }
});
export type ProjectBudgetCardFormData = Infer<typeof ProjectBudgetCardSchema>;
