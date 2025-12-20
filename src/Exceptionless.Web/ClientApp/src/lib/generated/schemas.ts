import type { infer as Infer } from "zod";

import {
  array,
  boolean,
  lazy,
  literal,
  number,
  object,
  record,
  string,
  union,
  unknown,
  enum as zodEnum,
} from "zod";

export const StackStatusSchema = zodEnum([
  "open",
  "fixed",
  "regressed",
  "snoozed",
  "ignored",
  "discarded",
]);
export const BillingStatusSchema = union([
  literal(0),
  literal(1),
  literal(2),
  literal(3),
  literal(4),
]);

export const BillingPlanSchema = object({
  id: string(),
  name: string(),
  description: string(),
  price: number(),
  max_projects: number(),
  max_users: number(),
  retention_days: number(),
  max_events_per_month: number(),
  has_premium_features: boolean(),
  is_hidden: boolean(),
});
export type BillingPlanFormData = Infer<typeof BillingPlanSchema>;

export const ChangePasswordModelSchema = object({
  current_password: string(),
  password: string(),
});
export type ChangePasswordModelFormData = Infer<
  typeof ChangePasswordModelSchema
>;

export const ChangePlanResultSchema = object({
  success: boolean(),
  message: string().nullable().optional(),
});
export type ChangePlanResultFormData = Infer<typeof ChangePlanResultSchema>;

export const ClientConfigurationSchema = object({
  version: number(),
  settings: record(string(), string()),
});
export type ClientConfigurationFormData = Infer<
  typeof ClientConfigurationSchema
>;

export const CountResultSchema = object({
  total: number(),
  aggregations: record(
    string(),
    lazy(() => IAggregateSchema),
  ).optional(),
  data: record(string(), unknown()).optional(),
});
export type CountResultFormData = Infer<typeof CountResultSchema>;

export const ExternalAuthInfoSchema = object({
  clientId: string(),
  code: string(),
  redirectUri: string(),
  inviteToken: string().nullable().optional(),
});
export type ExternalAuthInfoFormData = Infer<typeof ExternalAuthInfoSchema>;

export const IAggregateSchema = object({
  data: record(string(), unknown()).optional(),
});
export type IAggregateFormData = Infer<typeof IAggregateSchema>;

export const InviteSchema = object({
  token: string(),
  email_address: string(),
  date_added: string(),
});
export type InviteFormData = Infer<typeof InviteSchema>;

export const InvoiceSchema = object({
  id: string(),
  organization_id: string(),
  organization_name: string(),
  date: string(),
  paid: boolean(),
  total: number(),
  items: array(lazy(() => InvoiceLineItemSchema)),
});
export type InvoiceFormData = Infer<typeof InvoiceSchema>;

export const InvoiceGridModelSchema = object({
  id: string(),
  date: string(),
  paid: boolean(),
});
export type InvoiceGridModelFormData = Infer<typeof InvoiceGridModelSchema>;

export const InvoiceLineItemSchema = object({
  description: string(),
  date: string().nullable().optional(),
  amount: number(),
});
export type InvoiceLineItemFormData = Infer<typeof InvoiceLineItemSchema>;

export const LoginSchema = object({
  email: string(),
  password: string(),
  invite_token: string().nullable().optional(),
});
export type LoginFormData = Infer<typeof LoginSchema>;

export const NewOrganizationSchema = object({
  name: string(),
});
export type NewOrganizationFormData = Infer<typeof NewOrganizationSchema>;

export const NewProjectSchema = object({
  organization_id: string(),
  name: string(),
  delete_bot_data_enabled: boolean(),
});
export type NewProjectFormData = Infer<typeof NewProjectSchema>;

export const NewTokenSchema = object({
  organization_id: string(),
  project_id: string(),
  default_project_id: string().nullable().optional(),
  scopes: array(string()),
  expires_utc: string().nullable().optional(),
  notes: string().nullable().optional(),
});
export type NewTokenFormData = Infer<typeof NewTokenSchema>;

export const NewWebHookSchema = object({
  organization_id: string(),
  project_id: string(),
  url: string(),
  event_types: array(string()),
  version: string().nullable().optional(),
});
export type NewWebHookFormData = Infer<typeof NewWebHookSchema>;

export const NotificationSettingsSchema = object({
  send_daily_summary: boolean(),
  report_new_errors: boolean(),
  report_critical_errors: boolean(),
  report_event_regressions: boolean(),
  report_new_events: boolean(),
  report_critical_events: boolean(),
});
export type NotificationSettingsFormData = Infer<
  typeof NotificationSettingsSchema
>;

export const OAuthAccountSchema = object({
  provider: string(),
  provider_user_id: string(),
  username: string(),
  extra_data: record(string(), string()),
});
export type OAuthAccountFormData = Infer<typeof OAuthAccountSchema>;

export const PersistentEventSchema = object({
  id: string(),
  organization_id: string(),
  project_id: string(),
  stack_id: string(),
  is_first_occurrence: boolean(),
  created_utc: string(),
  idx: record(string(), unknown()),
  type: string().nullable().optional(),
  source: string().nullable().optional(),
  date: string(),
  tags: array(string()).nullable().optional(),
  message: string().nullable().optional(),
  geo: string().nullable().optional(),
  value: number().nullable().optional(),
  count: number().nullable().optional(),
  data: record(string(), unknown()).optional(),
  reference_id: string().nullable().optional(),
});
export type PersistentEventFormData = Infer<typeof PersistentEventSchema>;

export const ResetPasswordModelSchema = object({
  password_reset_token: string(),
  password: string(),
});
export type ResetPasswordModelFormData = Infer<typeof ResetPasswordModelSchema>;

export const SignupSchema = object({
  name: string(),
  email: string(),
  password: string(),
  invite_token: string().nullable().optional(),
});
export type SignupFormData = Infer<typeof SignupSchema>;

export const StackSchema = object({
  id: string(),
  organization_id: string(),
  project_id: string(),
  type: string(),
  status: StackStatusSchema,
  snooze_until_utc: string().nullable().optional(),
  signature_hash: string(),
  signature_info: record(string(), string()),
  fixed_in_version: string().nullable().optional(),
  date_fixed: string().nullable().optional(),
  title: string(),
  total_occurrences: number(),
  first_occurrence: string(),
  last_occurrence: string(),
  description: string().nullable().optional(),
  occurrences_are_critical: boolean(),
  references: array(string()),
  tags: array(string()),
  duplicate_signature: string(),
  created_utc: string(),
  updated_utc: string(),
  is_deleted: boolean(),
  allow_notifications: boolean(),
});
export type StackFormData = Infer<typeof StackSchema>;

export const StringStringValuesKeyValuePairSchema = object({
  key: string().nullable().optional(),
  value: array(string()),
});
export type StringStringValuesKeyValuePairFormData = Infer<
  typeof StringStringValuesKeyValuePairSchema
>;

export const StringValueFromBodySchema = object({
  value: string().nullable().optional(),
});
export type StringValueFromBodyFormData = Infer<
  typeof StringValueFromBodySchema
>;

export const TokenResultSchema = object({
  token: string(),
});
export type TokenResultFormData = Infer<typeof TokenResultSchema>;

export const UpdateEmailAddressResultSchema = object({
  is_verified: boolean(),
});
export type UpdateEmailAddressResultFormData = Infer<
  typeof UpdateEmailAddressResultSchema
>;

export const UsageHourInfoSchema = object({
  date: string(),
  total: number(),
  blocked: number(),
  discarded: number(),
  too_big: number(),
});
export type UsageHourInfoFormData = Infer<typeof UsageHourInfoSchema>;

export const UsageInfoSchema = object({
  date: string(),
  limit: number(),
  total: number(),
  blocked: number(),
  discarded: number(),
  too_big: number(),
});
export type UsageInfoFormData = Infer<typeof UsageInfoSchema>;

export const UserSchema = object({
  id: string(),
  organization_ids: array(string()),
  password: string().nullable().optional(),
  salt: string().nullable().optional(),
  password_reset_token: string().nullable().optional(),
  password_reset_token_expiration: string(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)),
  full_name: string(),
  email_address: string(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  verify_email_address_token: string().nullable().optional(),
  verify_email_address_token_expiration: string(),
  is_active: boolean(),
  roles: array(string()),
  created_utc: string(),
  updated_utc: string(),
});
export type UserFormData = Infer<typeof UserSchema>;

export const UserDescriptionSchema = object({
  email_address: string().nullable().optional(),
  description: string().nullable().optional(),
  data: record(string(), unknown()).optional(),
});
export type UserDescriptionFormData = Infer<typeof UserDescriptionSchema>;

export const ViewCurrentUserSchema = object({
  hash: string().nullable().optional(),
  has_local_account: boolean(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)),
  id: string(),
  organization_ids: array(string()),
  full_name: string(),
  email_address: string(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewCurrentUserFormData = Infer<typeof ViewCurrentUserSchema>;

export const ViewOrganizationSchema = object({
  id: string(),
  created_utc: string(),
  updated_utc: string(),
  name: string(),
  plan_id: string(),
  plan_name: string(),
  plan_description: string(),
  card_last4: string().nullable().optional(),
  subscribe_date: string().nullable().optional(),
  billing_change_date: string().nullable().optional(),
  billing_changed_by_user_id: string().nullable().optional(),
  billing_status: BillingStatusSchema,
  billing_price: number(),
  max_events_per_month: number(),
  bonus_events_per_month: number(),
  bonus_expiration: string().nullable().optional(),
  retention_days: number(),
  is_suspended: boolean(),
  suspension_code: string().nullable().optional(),
  suspension_notes: string().nullable().optional(),
  suspension_date: string().nullable().optional(),
  has_premium_features: boolean(),
  max_users: number(),
  max_projects: number(),
  project_count: number(),
  stack_count: number(),
  event_count: number(),
  invites: array(lazy(() => InviteSchema)),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
  data: record(string(), unknown()).optional(),
  is_throttled: boolean(),
  is_over_monthly_limit: boolean(),
  is_over_request_limit: boolean(),
});
export type ViewOrganizationFormData = Infer<typeof ViewOrganizationSchema>;

export const ViewProjectSchema = object({
  id: string(),
  created_utc: string(),
  organization_id: string(),
  organization_name: string(),
  name: string(),
  delete_bot_data_enabled: boolean(),
  data: record(string(), unknown()).optional(),
  promoted_tabs: array(string()),
  is_configured: boolean().nullable().optional(),
  stack_count: number(),
  event_count: number(),
  has_premium_features: boolean(),
  has_slack_integration: boolean(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
});
export type ViewProjectFormData = Infer<typeof ViewProjectSchema>;

export const ViewTokenSchema = object({
  id: string(),
  organization_id: string(),
  project_id: string(),
  user_id: string().nullable().optional(),
  default_project_id: string().nullable().optional(),
  scopes: array(string()),
  expires_utc: string().nullable().optional(),
  notes: string().nullable().optional(),
  is_disabled: boolean(),
  is_suspended: boolean(),
  created_utc: string(),
  updated_utc: string(),
});
export type ViewTokenFormData = Infer<typeof ViewTokenSchema>;

export const ViewUserSchema = object({
  id: string(),
  organization_ids: array(string()),
  full_name: string(),
  email_address: string(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewUserFormData = Infer<typeof ViewUserSchema>;

export const WebHookSchema = object({
  id: string(),
  organization_id: string(),
  project_id: string(),
  url: string(),
  event_types: array(string()),
  is_enabled: boolean(),
  version: string(),
  created_utc: string(),
});
export type WebHookFormData = Infer<typeof WebHookSchema>;

export const WorkInProgressResultSchema = object({
  workers: array(string()),
});
export type WorkInProgressResultFormData = Infer<
  typeof WorkInProgressResultSchema
>;
