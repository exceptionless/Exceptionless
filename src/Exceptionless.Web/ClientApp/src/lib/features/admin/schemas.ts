import { array, boolean, date, type infer as Infer, object, string } from 'zod';

export const RunMaintenanceJobSchema = object({
    confirmText: string().min(1),
    organizationId: string().optional(),
    utcEnd: date().optional(),
    utcStart: date().optional()
});
export type RunMaintenanceJobFormData = Infer<typeof RunMaintenanceJobSchema>;

export const OAuthApplicationSchema = object({
    client_id: string().min(3).max(2048),
    is_disabled: boolean(),
    name: string().min(1).max(200),
    notes: string().max(1000).optional(),
    redirect_uris: string()
        .min(1, 'Enter at least one redirect URI.')
        .refine(
            (value) =>
                value
                    .split(/\r?\n/)
                    .map((v) => v.trim())
                    .filter(Boolean)
                    .every((v) => {
                        try {
                            const uri = new URL(v);
                            return !uri.hash;
                        } catch {
                            return false;
                        }
                    }),
            'Each redirect URI must be an absolute URL without a fragment.'
        ),
    scopes: array(string()).min(1, 'Select at least one scope.')
});
export type OAuthApplicationFormData = Infer<typeof OAuthApplicationSchema>;
