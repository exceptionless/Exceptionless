import { array, boolean, date, type infer as Infer, object, string } from 'zod';

const authorizationCodeGrantType = 'authorization_code';
const deviceCodeGrantType = 'urn:ietf:params:oauth:grant-type:device_code';
const refreshTokenGrantType = 'refresh_token';
const offlineAccessScope = 'offline_access';

export const RunMaintenanceJobSchema = object({
    confirmText: string().min(1),
    organizationId: string().optional(),
    utcEnd: date().optional(),
    utcStart: date().optional()
});
export type RunMaintenanceJobFormData = Infer<typeof RunMaintenanceJobSchema>;

export const OAuthApplicationSchema = object({
    client_id: string().min(3).max(2048),
    grant_types: array(string()).min(1, 'Select at least one grant type.'),
    is_disabled: boolean(),
    name: string().min(1).max(200),
    notes: string().max(1000).optional(),
    redirect_uris: string().refine(
        (value) =>
            !value.trim() ||
            value
                .split(/\r?\n/)
                .map((v) => v.trim())
                .filter(Boolean)
                .every((v) => {
                    try {
                        const uri = new URL(v);
                        const isHttps = uri.protocol === 'https:';
                        const isLoopbackHttp =
                            uri.protocol === 'http:' && (uri.hostname === 'localhost' || uri.hostname === '127.0.0.1' || uri.hostname === '[::1]');
                        return !uri.hash && (isHttps || isLoopbackHttp);
                    } catch {
                        return false;
                    }
                }),
        'Each redirect URI must be HTTPS or loopback HTTP without a fragment.'
    ),
    scopes: array(string()).min(1, 'Select at least one scope.')
})
    .refine((value) => value.grant_types.includes(authorizationCodeGrantType) || value.grant_types.includes(deviceCodeGrantType), {
        message: 'Select authorization code or device code.',
        path: ['grant_types']
    })
    .refine((value) => !value.grant_types.includes(authorizationCodeGrantType) || value.redirect_uris.trim().length > 0, {
        message: 'Enter at least one redirect URI for authorization-code clients.',
        path: ['redirect_uris']
    })
    .refine((value) => !value.scopes.includes(offlineAccessScope) || value.grant_types.includes(refreshTokenGrantType), {
        message: 'Offline access requires the refresh token grant type.',
        path: ['scopes']
    });
export type OAuthApplicationFormData = Infer<typeof OAuthApplicationSchema>;
