import { boolean, date, type infer as Infer, number, object, string, enum as zodEnum } from 'zod';

import { SuspensionCode } from './models';

export { type NewOrganizationFormData, NewOrganizationSchema } from '$generated/schemas';

export const BudgetAlertCardSchema = object({
    enabled: boolean(),
    thresholds: string()
}).superRefine((settings, context) => {
    const values = settings.thresholds
        .split(',')
        .map((value) => value.trim())
        .filter(Boolean);

    if (settings.enabled && values.length === 0) {
        context.addIssue({ code: 'custom', message: 'Enter at least one threshold.', path: ['thresholds'] });
        return;
    }

    if (values.some((value) => !/^\d+$/.test(value) || Number(value) < 1 || Number(value) > 99)) {
        context.addIssue({ code: 'custom', message: 'Use whole-number percentages from 1 to 99.', path: ['thresholds'] });
    }
});
export type BudgetAlertCardFormData = Infer<typeof BudgetAlertCardSchema>;

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
