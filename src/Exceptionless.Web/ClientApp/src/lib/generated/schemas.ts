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
  price: number().optional(),
  max_projects: number().optional(),
  max_users: number().optional(),
  retention_days: number().optional(),
  max_events_per_month: number().optional(),
  has_premium_features: boolean().optional(),
  is_hidden: boolean().optional(),
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
  success: boolean().optional(),
  message: string().nullable().optional(),
});
export type ChangePlanResultFormData = Infer<typeof ChangePlanResultSchema>;

export const ClientConfigurationSchema = object({
  version: number().optional(),
  settings: record(string(), string()).optional(),
});
export type ClientConfigurationFormData = Infer<
  typeof ClientConfigurationSchema
>;

export const CountResultSchema = object({
  total: number().optional(),
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
  items: array(lazy(() => InvoiceLineItemSchema)).optional(),
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
  name: string().optional(),
});
export type NewOrganizationFormData = Infer<typeof NewOrganizationSchema>;

export const NewProjectSchema = object({
  organization_id: string().optional(),
  name: string().optional(),
  delete_bot_data_enabled: boolean().optional(),
});
export type NewProjectFormData = Infer<typeof NewProjectSchema>;

export const NewTokenSchema = object({
  organization_id: string().optional(),
  project_id: string().optional(),
  default_project_id: string().nullable().optional(),
  scopes: array(string()).optional(),
  expires_utc: string().nullable().optional(),
  notes: string().nullable().optional(),
});
export type NewTokenFormData = Infer<typeof NewTokenSchema>;

export const NewWebHookSchema = object({
  organization_id: string().optional(),
  project_id: string().optional(),
  url: string().optional(),
  event_types: array(string()).optional(),
  version: string().nullable().optional(),
});
export type NewWebHookFormData = Infer<typeof NewWebHookSchema>;

export const NotificationSettingsSchema = object({
  send_daily_summary: boolean().optional(),
  report_new_errors: boolean().optional(),
  report_critical_errors: boolean().optional(),
  report_event_regressions: boolean().optional(),
  report_new_events: boolean().optional(),
  report_critical_events: boolean().optional(),
});
export type NotificationSettingsFormData = Infer<
  typeof NotificationSettingsSchema
>;

export const OAuthAccountSchema = object({
  provider: string(),
  provider_user_id: string(),
  username: string(),
  extra_data: record(string(), string()).optional(),
});
export type OAuthAccountFormData = Infer<typeof OAuthAccountSchema>;

export const PersistentEventSchema = object({
  id: string().optional(),
  organization_id: string().optional(),
  project_id: string().optional(),
  stack_id: string().optional(),
  is_first_occurrence: boolean().optional(),
  created_utc: string().optional(),
  idx: record(string(), unknown()).optional(),
  type: string().nullable().optional(),
  source: string().nullable().optional(),
  date: string().optional(),
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
  id: string().optional(),
  organization_id: string().optional(),
  project_id: string().optional(),
  type: string().optional(),
  status: StackStatusSchema.optional(),
  snooze_until_utc: string().nullable().optional(),
  signature_hash: string().optional(),
  signature_info: record(string(), string()).optional(),
  fixed_in_version: string().nullable().optional(),
  date_fixed: string().nullable().optional(),
  title: string().optional(),
  total_occurrences: number().optional(),
  first_occurrence: string().optional(),
  last_occurrence: string().optional(),
  description: string().nullable().optional(),
  occurrences_are_critical: boolean().optional(),
  references: array(string()).optional(),
  tags: array(string()).optional(),
  duplicate_signature: string().optional(),
  created_utc: string().optional(),
  updated_utc: string().optional(),
  is_deleted: boolean().optional(),
  allow_notifications: boolean().optional(),
});
export type StackFormData = Infer<typeof StackSchema>;

export const StringStringValuesKeyValuePairSchema = object({
  key: string().nullable().optional(),
  value: array(string()).optional(),
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
  date: string().optional(),
  total: number().optional(),
  blocked: number().optional(),
  discarded: number().optional(),
  too_big: number().optional(),
});
export type UsageHourInfoFormData = Infer<typeof UsageHourInfoSchema>;

export const UsageInfoSchema = object({
  date: string().optional(),
  limit: number().optional(),
  total: number().optional(),
  blocked: number().optional(),
  discarded: number().optional(),
  too_big: number().optional(),
});
export type UsageInfoFormData = Infer<typeof UsageInfoSchema>;

export const UserSchema = object({
  id: string().optional(),
  organization_ids: array(string()).optional(),
  password: string().nullable().optional(),
  salt: string().nullable().optional(),
  password_reset_token: string().nullable().optional(),
  password_reset_token_expiration: string().optional(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)).optional(),
  full_name: string(),
  email_address: string(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  verify_email_address_token: string().nullable().optional(),
  verify_email_address_token_expiration: string().optional(),
  is_active: boolean().optional(),
  roles: array(string()).optional(),
  created_utc: string().optional(),
  updated_utc: string().optional(),
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
  has_local_account: boolean().optional(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)).optional(),
  id: string().optional(),
  organization_ids: array(string()).optional(),
  full_name: string().optional(),
  email_address: string().optional(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  is_active: boolean().optional(),
  is_invite: boolean().optional(),
  roles: array(string()).optional(),
});
export type ViewCurrentUserFormData = Infer<typeof ViewCurrentUserSchema>;

export const ViewOrganizationSchema = object({
  id: string().optional(),
  created_utc: string().optional(),
  updated_utc: string().optional(),
  name: string().optional(),
  plan_id: string().optional(),
  plan_name: string().optional(),
  plan_description: string().optional(),
  card_last4: string().nullable().optional(),
  subscribe_date: string().nullable().optional(),
  billing_change_date: string().nullable().optional(),
  billing_changed_by_user_id: string().nullable().optional(),
  billing_status: BillingStatusSchema.optional(),
  billing_price: number().optional(),
  max_events_per_month: number().optional(),
  bonus_events_per_month: number().optional(),
  bonus_expiration: string().nullable().optional(),
  retention_days: number().optional(),
  is_suspended: boolean().optional(),
  suspension_code: string().nullable().optional(),
  suspension_notes: string().nullable().optional(),
  suspension_date: string().nullable().optional(),
  has_premium_features: boolean().optional(),
  max_users: number().optional(),
  max_projects: number().optional(),
  project_count: number().optional(),
  stack_count: number().optional(),
  event_count: number().optional(),
  invites: array(lazy(() => InviteSchema)).optional(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)).optional(),
  usage: array(lazy(() => UsageInfoSchema)).optional(),
  data: record(string(), unknown()).optional(),
  is_throttled: boolean().optional(),
  is_over_monthly_limit: boolean().optional(),
  is_over_request_limit: boolean().optional(),
});
export type ViewOrganizationFormData = Infer<typeof ViewOrganizationSchema>;

export const ViewProjectSchema = object({
  id: string().optional(),
  created_utc: string().optional(),
  organization_id: string().optional(),
  organization_name: string().optional(),
  name: string().optional(),
  delete_bot_data_enabled: boolean().optional(),
  data: record(string(), unknown()).optional(),
  promoted_tabs: array(string()).optional(),
  is_configured: boolean().nullable().optional(),
  stack_count: number().optional(),
  event_count: number().optional(),
  has_premium_features: boolean().optional(),
  has_slack_integration: boolean().optional(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)).optional(),
  usage: array(lazy(() => UsageInfoSchema)).optional(),
});
export type ViewProjectFormData = Infer<typeof ViewProjectSchema>;

export const ViewTokenSchema = object({
  id: string().optional(),
  organization_id: string().optional(),
  project_id: string().optional(),
  user_id: string().nullable().optional(),
  default_project_id: string().nullable().optional(),
  scopes: array(string()).optional(),
  expires_utc: string().nullable().optional(),
  notes: string().nullable().optional(),
  is_disabled: boolean().optional(),
  is_suspended: boolean().optional(),
  created_utc: string().optional(),
  updated_utc: string().optional(),
});
export type ViewTokenFormData = Infer<typeof ViewTokenSchema>;

export const ViewUserSchema = object({
  id: string().optional(),
  organization_ids: array(string()).optional(),
  full_name: string().optional(),
  email_address: string().optional(),
  email_notifications_enabled: boolean().optional(),
  is_email_address_verified: boolean().optional(),
  is_active: boolean().optional(),
  is_invite: boolean().optional(),
  roles: array(string()).optional(),
});
export type ViewUserFormData = Infer<typeof ViewUserSchema>;

export const WebHookSchema = object({
  id: string().optional(),
  organization_id: string().optional(),
  project_id: string().optional(),
  url: string().optional(),
  event_types: array(string()).optional(),
  is_enabled: boolean().optional(),
  version: string().optional(),
  created_utc: string().optional(),
});
export type WebHookFormData = Infer<typeof WebHookSchema>;

export const WorkInProgressResultSchema = object({
  workers: array(string()).optional(),
});
export type WorkInProgressResultFormData = Infer<
  typeof WorkInProgressResultSchema
>;
