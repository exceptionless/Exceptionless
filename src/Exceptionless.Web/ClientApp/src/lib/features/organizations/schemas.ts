import { date, type infer as Infer, number, object, string, enum as zodEnum, array, boolean } from 'zod';

import { SuspensionCode } from './models';

export { type NewOrganizationFormData, NewOrganizationSchema } from '$generated/schemas';

export const BudgetAlertSettingsSchema = object({
    enabled: boolean(),
    thresholds: array(number().int().min(1).max(100))
});
export type BudgetAlertSettingsFormData = Infer<typeof BudgetAlertSettingsSchema>;

export const UpdateOrganizationSchema = object({
    name: string().min(1, 'Name is required').optional(),
    budget_alert_settings: BudgetAlertSettingsSchema.nullable().optional()
});
export type UpdateOrganizationFormData = Infer<typeof UpdateOrganizationSchema>;

export const SetBonusOrganizationSchema = object({
    bonusEvents: number().int('Bonus events must be a whole number'),
    expires: date().optional()
});
export type SetBonusOrganizationFormData = Infer<typeof SetBonusOrganizationSchema>;

export const SuspendOrganizationSchema = object({
    code: zodEnum(SuspensionCode, { message: 'Suspension code is required' }),
    notes: string().optional()
});
export type SuspendOrganizationFormData = Infer<typeof SuspendOrganizationSchema>;
