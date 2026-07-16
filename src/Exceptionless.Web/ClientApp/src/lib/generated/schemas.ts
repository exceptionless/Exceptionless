import type { infer as Infer } from "zod";

import {
  array,
  boolean,
  email,
  int,
  int32,
  iso,
  lazy,
  literal,
  number,
  object,
  record,
  string,
  union,
  unknown,
  url,
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
  id: string().min(1, "Id is required"),
  name: string().min(1, "Name is required"),
  description: string().min(1, "Description is required"),
  price: number(),
  max_projects: int32(),
  max_users: int32(),
  retention_days: int32(),
  max_events_per_month: int32(),
  has_premium_features: boolean(),
  is_hidden: boolean(),
});
export type BillingPlanFormData = Infer<typeof BillingPlanSchema>;

export const ChangePasswordModelSchema = object({
  current_password: string()
    .min(6, "Current password must be at least 6 characters")
    .max(100, "Current password must be at most 100 characters"),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
});
export type ChangePasswordModelFormData = Infer<
  typeof ChangePasswordModelSchema
>;

export const ChangePlanRequestSchema = object({
  plan_id: string().min(1, "Plan id is required"),
  stripe_token: string()
    .min(1, "Stripe token is required")
    .nullable()
    .optional(),
  last4: string().min(1, '"last4" is required').nullable().optional(),
  coupon_id: string().min(1, "Coupon id is required").nullable().optional(),
});
export type ChangePlanRequestFormData = Infer<typeof ChangePlanRequestSchema>;

export const ChangePlanResultSchema = object({
  success: boolean(),
  message: string().min(1, "Message is required").nullable().optional(),
});
export type ChangePlanResultFormData = Infer<typeof ChangePlanResultSchema>;

export const ClientConfigurationSchema = object({
  version: int32(),
  settings: record(string(), string()),
});
export type ClientConfigurationFormData = Infer<
  typeof ClientConfigurationSchema
>;

export const CountResultSchema = object({
  total: int(),
  aggregations: record(
    string(),
    lazy(() => IAggregateSchema),
  ),
  data: record(string(), unknown()).nullable(),
});
export type CountResultFormData = Infer<typeof CountResultSchema>;

export const CustomFieldDefinitionResponseSchema = object({
  id: string().min(1, "Id is required"),
  name: string().min(1, "Name is required"),
  description: string().min(1, "Description is required").nullable().optional(),
  index_type: string().min(1, "Index type is required"),
  display_order: int32(),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type CustomFieldDefinitionResponseFormData = Infer<
  typeof CustomFieldDefinitionResponseSchema
>;

export const ExternalAuthInfoSchema = object({
  clientId: string().min(1, "Client id is required"),
  code: string().min(1, "Code is required"),
  redirectUri: url(),
  inviteToken: string()
    .min(1, "Invite token is required")
    .nullable()
    .optional(),
});
export type ExternalAuthInfoFormData = Infer<typeof ExternalAuthInfoSchema>;

export const IAggregateSchema = object({
  data: record(string(), unknown()).optional(),
});
export type IAggregateFormData = Infer<typeof IAggregateSchema>;

export const InviteSchema = object({
  token: string().min(1, "Token is required"),
  email_address: email(),
  date_added: iso.datetime(),
});
export type InviteFormData = Infer<typeof InviteSchema>;

export const InvoiceSchema = object({
  id: string().min(1, "Id is required"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  organization_name: string().min(1, "Organization name is required"),
  date: iso.datetime(),
  paid: boolean(),
  total: number(),
  items: array(lazy(() => InvoiceLineItemSchema)),
});
export type InvoiceFormData = Infer<typeof InvoiceSchema>;

export const InvoiceGridModelSchema = object({
  id: string().min(1, "Id is required"),
  date: iso.datetime(),
  paid: boolean(),
});
export type InvoiceGridModelFormData = Infer<typeof InvoiceGridModelSchema>;

export const InvoiceLineItemSchema = object({
  description: string().min(1, "Description is required"),
  date: iso.datetime().nullable().optional(),
  amount: number(),
});
export type InvoiceLineItemFormData = Infer<typeof InvoiceLineItemSchema>;

export const LoginSchema = object({
  email: email(),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
  invite_token: string()
    .length(40, "Invite token must be exactly 40 characters")
    .nullable()
    .optional(),
});
export type LoginFormData = Infer<typeof LoginSchema>;

export const NewCustomFieldDefinitionSchema = object({
  name: string()
    .min(1, "Name is required")
    .max(100, "Name must be at most 100 characters"),
  index_type: string()
    .min(1, "Index type is required")
    .max(20, "Index type must be at most 20 characters"),
  description: string()
    .min(1, "Description is required")
    .max(500, "Description must be at most 500 characters")
    .nullable()
    .optional(),
  display_order: int32().nullable().optional(),
});
export type NewCustomFieldDefinitionFormData = Infer<
  typeof NewCustomFieldDefinitionSchema
>;

export const NewOrganizationSchema = object({
  name: string().min(1, "Name is required"),
});
export type NewOrganizationFormData = Infer<typeof NewOrganizationSchema>;

export const NewProjectSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean(),
  promoted_tabs: array(string()).nullable().optional(),
});
export type NewProjectFormData = Infer<typeof NewProjectSchema>;

export const NewSavedViewSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  name: string()
    .min(1, "Name is required")
    .max(100, "Name must be at most 100 characters"),
  filter: string()
    .min(1, "Filter is required")
    .max(2000, "Filter must be at most 2000 characters")
    .nullable()
    .optional(),
  time: string()
    .min(1, "Time is required")
    .max(100, "Time must be at most 100 characters")
    .nullable()
    .optional(),
  sort: string()
    .min(1, "Sort is required")
    .max(100, "Sort must be at most 100 characters")
    .nullable()
    .optional(),
  slug: string()
    .min(1, "Slug is required")
    .max(100, "Slug must be at most 100 characters")
    .regex(
      /^(?![a-f0-9]{24}$)[a-z0-9]+(?:-[a-z0-9]+)*$/,
      "Slug has invalid format",
    )
    .nullable()
    .optional(),
  view_type: string().min(1, "View type is required"),
  filter_definitions: string()
    .min(1, "Filter definitions is required")
    .max(100000, "Filter definitions must be at most 100000 characters")
    .nullable()
    .optional(),
  columns: record(string(), boolean()).nullable().optional(),
  column_order: array(string()).nullable().optional(),
  show_stats: boolean().nullable().optional(),
  show_chart: boolean().nullable().optional(),
  is_private: boolean().nullable().optional(),
});
export type NewSavedViewFormData = Infer<typeof NewSavedViewSchema>;

export const NewTokenSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  default_project_id: string()
    .length(24, "Default project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Default project id has invalid format")
    .nullable()
    .optional(),
  scopes: array(string()),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
});
export type NewTokenFormData = Infer<typeof NewTokenSchema>;

export const NewWebHookSchema = object({
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  url: url(),
  event_types: array(
    zodEnum([
      "CriticalError",
      "CriticalEvent",
      "NewError",
      "NewEvent",
      "StackPromoted",
      "StackRegression",
    ]),
  ),
  version: string()
    .regex(/^\d+(\.\d+){1,3}$/, "Version has invalid format")
    .nullable()
    .optional(),
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
  provider: string().min(1, "Provider is required"),
  provider_user_id: string().min(1, "Provider user id is required"),
  username: string().min(1, "Username is required"),
  extra_data: record(string(), string()),
});
export type OAuthAccountFormData = Infer<typeof OAuthAccountSchema>;

export const OAuthAuthorizationServerMetadataSchema = object({
  issuer: string().min(1, "Issuer is required"),
  authorization_endpoint: string().min(1, "Authorization endpoint is required"),
  token_endpoint: string().min(1, "Token endpoint is required"),
  registration_endpoint: string().min(1, "Registration endpoint is required"),
  revocation_endpoint: string().min(1, "Revocation endpoint is required"),
  grant_types_supported: array(string()),
  response_types_supported: array(string()),
  code_challenge_methods_supported: array(string()),
  token_endpoint_auth_methods_supported: array(string()),
  scopes_supported: array(string()),
  resource_documentation: string().min(1, "Resource documentation is required"),
  client_id_metadata_document_supported: boolean(),
});
export type OAuthAuthorizationServerMetadataFormData = Infer<
  typeof OAuthAuthorizationServerMetadataSchema
>;

export const OAuthAuthorizeConsentResponseSchema = object({
  client_id: string().min(1, "Client id is required"),
  client_name: string().min(1, "Client name is required"),
  redirect_uri: string().min(1, "Redirect uri is required"),
  resource: string().min(1, "Resource is required"),
  scopes: array(string()),
  required_scopes: array(string()),
});
export type OAuthAuthorizeConsentResponseFormData = Infer<
  typeof OAuthAuthorizeConsentResponseSchema
>;

export const OAuthAuthorizeFormSchema = object({
  client_id: string().min(1, "Client id is required"),
  response_type: string().min(1, "Response type is required"),
  redirect_uri: string().min(1, "Redirect uri is required"),
  scope: string().min(1, "Scope is required").nullable().optional(),
  state: string().min(1, "State is required").nullable().optional(),
  code_challenge: string().min(1, "Code challenge is required"),
  code_challenge_method: string().min(1, "Code challenge method is required"),
  resource: string().min(1, "Resource is required").nullable().optional(),
  organization_ids: array(string()).nullable().optional(),
});
export type OAuthAuthorizeFormFormData = Infer<typeof OAuthAuthorizeFormSchema>;

export const OAuthAuthorizeResponseSchema = object({
  redirect_uri: string().min(1, "Redirect uri is required"),
});
export type OAuthAuthorizeResponseFormData = Infer<
  typeof OAuthAuthorizeResponseSchema
>;

export const OAuthClientRegistrationRequestSchema = object({
  redirect_uris: array(string()).nullable().optional(),
  client_name: string().min(1, "Client name is required").nullable().optional(),
  scope: string().min(1, "Scope is required").nullable().optional(),
  grant_types: array(string()).nullable().optional(),
  response_types: array(string()).nullable().optional(),
  token_endpoint_auth_method: string()
    .min(1, "Token endpoint auth method is required")
    .nullable()
    .optional(),
});
export type OAuthClientRegistrationRequestFormData = Infer<
  typeof OAuthClientRegistrationRequestSchema
>;

export const OAuthClientRegistrationResponseSchema = object({
  client_id: string().min(1, "Client id is required"),
  client_name: string().min(1, "Client name is required"),
  redirect_uris: array(string()),
  grant_types: array(string()),
  response_types: array(string()),
  scope: string().min(1, "Scope is required"),
  token_endpoint_auth_method: string().min(
    1,
    "Token endpoint auth method is required",
  ),
  client_id_issued_at: int(),
});
export type OAuthClientRegistrationResponseFormData = Infer<
  typeof OAuthClientRegistrationResponseSchema
>;

export const OAuthErrorResponseSchema = object({
  error: string().min(1, "Error is required"),
  error_description: string()
    .min(1, "Error description is required")
    .nullable()
    .optional(),
});
export type OAuthErrorResponseFormData = Infer<typeof OAuthErrorResponseSchema>;

export const OAuthProtectedResourceMetadataSchema = object({
  resource: string().min(1, "Resource is required"),
  authorization_servers: array(string()),
  scopes_supported: array(string()),
  bearer_methods_supported: array(string()),
  resource_documentation: string().min(1, "Resource documentation is required"),
});
export type OAuthProtectedResourceMetadataFormData = Infer<
  typeof OAuthProtectedResourceMetadataSchema
>;

export const OAuthRevokeFormSchema = object({
  token: string().min(1, "Token is required").nullable().optional(),
  client_id: string().min(1, "Client id is required").nullable().optional(),
});
export type OAuthRevokeFormFormData = Infer<typeof OAuthRevokeFormSchema>;

export const OAuthTokenFormSchema = object({
  grant_type: string().min(1, "Grant type is required"),
  code: string().min(1, "Code is required").nullable().optional(),
  redirect_uri: string()
    .min(1, "Redirect uri is required")
    .nullable()
    .optional(),
  client_id: string().min(1, "Client id is required").nullable().optional(),
  code_verifier: string()
    .min(1, "Code verifier is required")
    .nullable()
    .optional(),
  refresh_token: string()
    .min(1, "Refresh token is required")
    .nullable()
    .optional(),
  resource: string().min(1, "Resource is required").nullable().optional(),
});
export type OAuthTokenFormFormData = Infer<typeof OAuthTokenFormSchema>;

export const OAuthTokenResponseSchema = object({
  access_token: string().min(1, "Access token is required"),
  token_type: string().min(1, "Token type is required"),
  expires_in: int32(),
  refresh_token: string()
    .min(1, "Refresh token is required")
    .nullable()
    .optional(),
  scope: string().min(1, "Scope is required").nullable().optional(),
  resource: string().min(1, "Resource is required").nullable().optional(),
});
export type OAuthTokenResponseFormData = Infer<typeof OAuthTokenResponseSchema>;

export const PersistentEventSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  stack_id: string()
    .length(24, "Stack id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Stack id has invalid format"),
  is_first_occurrence: boolean(),
  created_utc: iso.datetime(),
  idx: record(string(), unknown()).nullable().optional(),
  type: string()
    .min(1, "Type is required")
    .max(100, "Type must be at most 100 characters")
    .nullable(),
  source: string()
    .min(1, "Source is required")
    .max(2000, "Source must be at most 2000 characters")
    .nullable()
    .optional(),
  date: iso.datetime(),
  tags: array(string()).nullable().optional(),
  message: string()
    .min(1, "Message is required")
    .max(2000, "Message must be at most 2000 characters")
    .nullable()
    .optional(),
  geo: string().min(1, "Geo is required").nullable().optional(),
  value: number().nullable().optional(),
  count: int32().nullable().optional(),
  data: record(string(), unknown()).nullable().optional(),
  reference_id: string()
    .min(1, "Reference id is required")
    .nullable()
    .optional(),
});
export type PersistentEventFormData = Infer<typeof PersistentEventSchema>;

export const PredefinedSavedViewDefinitionSchema = object({
  key: string().min(1, "Key is required"),
  name: string().min(1, "Name is required"),
  slug: string().min(1, "Slug is required"),
  viewType: string().min(1, "View type is required"),
  filter: string().min(1, "Filter is required").nullable().optional(),
  time: string().min(1, "Time is required").nullable().optional(),
  sort: string().min(1, "Sort is required").nullable().optional(),
  filterDefinitions: unknown().optional(),
  columns: record(string(), boolean()).nullable().optional(),
  columnOrder: array(string()).nullable().optional(),
  showStats: boolean().nullable().optional(),
  showChart: boolean().nullable().optional(),
});
export type PredefinedSavedViewDefinitionFormData = Infer<
  typeof PredefinedSavedViewDefinitionSchema
>;

export const ProblemDetailsSchema = object({
  type: string().min(1, "Type is required").nullable().optional(),
  title: string().min(1, "Title is required").nullable().optional(),
  status: int32().nullable().optional(),
  detail: string().min(1, "Detail is required").nullable().optional(),
  instance: string().min(1, "Instance is required").nullable().optional(),
});
export type ProblemDetailsFormData = Infer<typeof ProblemDetailsSchema>;

export const ResetPasswordModelSchema = object({
  password_reset_token: string().length(
    40,
    "Password reset token must be exactly 40 characters",
  ),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
});
export type ResetPasswordModelFormData = Infer<typeof ResetPasswordModelSchema>;

export const SignupSchema = object({
  name: string().min(1, "Name is required"),
  email: email(),
  password: string()
    .min(6, "Password must be at least 6 characters")
    .max(100, "Password must be at most 100 characters"),
  invite_token: string()
    .length(40, "Invite token must be exactly 40 characters")
    .nullable()
    .optional(),
});
export type SignupFormData = Infer<typeof SignupSchema>;

export const StackSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  type: string()
    .min(1, "Type is required")
    .max(100, "Type must be at most 100 characters"),
  status: StackStatusSchema,
  snooze_until_utc: iso.datetime().nullable().optional(),
  signature_hash: string().min(1, "Signature hash is required"),
  signature_info: record(string(), string()),
  fixed_in_version: string().nullable().optional(),
  date_fixed: iso.datetime().nullable().optional(),
  title: string()
    .min(1, "Title is required")
    .max(1000, "Title must be at most 1000 characters"),
  total_occurrences: int32(),
  first_occurrence: iso.datetime(),
  last_occurrence: iso.datetime(),
  description: string().min(1, "Description is required").nullable().optional(),
  occurrences_are_critical: boolean(),
  references: array(string()),
  tags: array(string()),
  duplicate_signature: string().min(1, "Duplicate signature is required"),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
  is_deleted: boolean(),
  allow_notifications: boolean(),
});
export type StackFormData = Infer<typeof StackSchema>;

export const StringValueFromBodySchema = object({
  value: string().min(1, "Value is required").nullable(),
});
export type StringValueFromBodyFormData = Infer<
  typeof StringValueFromBodySchema
>;

export const TokenResultSchema = object({
  token: string().min(1, "Token is required"),
});
export type TokenResultFormData = Infer<typeof TokenResultSchema>;

export const UpdateCustomFieldDefinitionSchema = object({
  description: string()
    .min(1, "Description is required")
    .max(500, "Description must be at most 500 characters")
    .nullable()
    .optional(),
  display_order: int32().nullable().optional(),
});
export type UpdateCustomFieldDefinitionFormData = Infer<
  typeof UpdateCustomFieldDefinitionSchema
>;

export const UpdateEmailAddressResultSchema = object({
  is_verified: boolean(),
});
export type UpdateEmailAddressResultFormData = Infer<
  typeof UpdateEmailAddressResultSchema
>;

export const UpdateEventSchema = object({
  email_address: email().nullable().optional(),
  description: string().min(1, "Description is required").nullable().optional(),
});
export type UpdateEventFormData = Infer<typeof UpdateEventSchema>;

export const UpdateProjectSchema = object({
  name: string().min(1, "Name is required").optional(),
  delete_bot_data_enabled: boolean().optional(),
  promoted_tabs: array(string()).nullable().optional(),
});
export type UpdateProjectFormData = Infer<typeof UpdateProjectSchema>;

export const UpdateSavedViewSchema = object({
  name: string().min(1, "Name is required").nullable().optional(),
  filter: string().min(1, "Filter is required").nullable().optional(),
  time: string().min(1, "Time is required").nullable().optional(),
  sort: string().min(1, "Sort is required").nullable().optional(),
  slug: string().min(1, "Slug is required").nullable().optional(),
  filter_definitions: string()
    .min(1, "Filter definitions is required")
    .nullable()
    .optional(),
  columns: record(string(), boolean()).nullable().optional(),
  column_order: array(string()).nullable().optional(),
  show_stats: boolean().nullable().optional(),
  show_chart: boolean().nullable().optional(),
});
export type UpdateSavedViewFormData = Infer<typeof UpdateSavedViewSchema>;

export const UpdateTokenSchema = object({
  is_disabled: boolean().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
});
export type UpdateTokenFormData = Infer<typeof UpdateTokenSchema>;

export const UpdateUserSchema = object({
  full_name: string().min(1, "Full name is required").optional(),
  email_notifications_enabled: boolean().optional(),
});
export type UpdateUserFormData = Infer<typeof UpdateUserSchema>;

export const UsageHourInfoSchema = object({
  date: iso.datetime(),
  total: int32(),
  blocked: int32(),
  discarded: int32(),
  too_big: int32(),
  deleted: int32(),
});
export type UsageHourInfoFormData = Infer<typeof UsageHourInfoSchema>;

export const UsageInfoSchema = object({
  date: iso.datetime(),
  limit: int32(),
  total: int32(),
  blocked: int32(),
  discarded: int32(),
  too_big: int32(),
  deleted: int32(),
});
export type UsageInfoFormData = Infer<typeof UsageInfoSchema>;

export const UserSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()),
  password: string().min(1, "Password is required").nullable().optional(),
  salt: string().min(1, "Salt is required").nullable().optional(),
  password_reset_token: string()
    .min(1, "Password reset token is required")
    .nullable()
    .optional(),
  password_reset_token_expiration: iso.datetime(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  avatar_file_name: string()
    .min(1, "Avatar file name is required")
    .max(2000, "Avatar file name must be at most 2000 characters")
    .nullable()
    .optional(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  verify_email_address_token: string()
    .min(1, "Verify email address token is required")
    .nullable()
    .optional(),
  verify_email_address_token_expiration: iso.datetime(),
  is_active: boolean(),
  roles: array(string()),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type UserFormData = Infer<typeof UserSchema>;

export const UserDescriptionSchema = object({
  email_address: email().nullable().optional(),
  description: string().min(1, "Description is required").nullable().optional(),
  data: record(string(), unknown()).nullable().optional(),
});
export type UserDescriptionFormData = Infer<typeof UserDescriptionSchema>;

export const ViewCurrentUserSchema = object({
  hash: string().min(1, "Hash is required").nullable().optional(),
  has_local_account: boolean(),
  o_auth_accounts: array(lazy(() => OAuthAccountSchema)),
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  avatar_url: url().nullable().optional(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewCurrentUserFormData = Infer<typeof ViewCurrentUserSchema>;

export const ViewOAuthGrantSchema = object({
  id: string().min(1, "Id is required"),
  client_id: string().min(1, "Client id is required"),
  application_name: string().min(1, "Application name is required"),
  is_application_disabled: boolean(),
  scopes: array(string()),
  organization_ids: array(string()),
  resources: array(lazy(() => ViewOAuthGrantResourceSchema)),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
  expires_utc: iso.datetime().nullable().optional(),
  refresh_expires_utc: iso.datetime().nullable().optional(),
});
export type ViewOAuthGrantFormData = Infer<typeof ViewOAuthGrantSchema>;

export const ViewOAuthGrantResourceSchema = object({
  resource: string().min(1, "Resource is required"),
  scopes: array(string()),
  organization_ids: array(string()),
});
export type ViewOAuthGrantResourceFormData = Infer<
  typeof ViewOAuthGrantResourceSchema
>;

export const ViewOrganizationSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
  name: string().min(1, "Name is required"),
  icon_url: url().nullable().optional(),
  plan_id: string().min(1, "Plan id is required"),
  plan_name: string().min(1, "Plan name is required"),
  plan_description: string().min(1, "Plan description is required"),
  card_last4: string().min(1, '"card last4" is required').nullable().optional(),
  subscribe_date: iso.datetime().nullable().optional(),
  billing_change_date: iso.datetime().nullable().optional(),
  billing_changed_by_user_id: string()
    .length(24, "Billing changed by user id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Billing changed by user id has invalid format")
    .nullable()
    .optional(),
  billing_status: BillingStatusSchema,
  billing_price: number(),
  max_events_per_month: int32(),
  bonus_events_per_month: int32(),
  bonus_expiration: iso.datetime().nullable().optional(),
  retention_days: int32(),
  is_suspended: boolean(),
  suspension_code: string()
    .min(1, "Suspension code is required")
    .nullable()
    .optional(),
  suspension_notes: string()
    .min(1, "Suspension notes is required")
    .nullable()
    .optional(),
  suspension_date: iso.datetime().nullable().optional(),
  has_premium_features: boolean(),
  features: array(string()),
  max_users: int32(),
  max_projects: int32(),
  project_count: int(),
  stack_count: int(),
  event_count: int(),
  invites: array(lazy(() => InviteSchema)),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
  data: record(string(), unknown()).nullable().optional(),
  is_throttled: boolean(),
  is_over_monthly_limit: boolean(),
  is_over_request_limit: boolean(),
});
export type ViewOrganizationFormData = Infer<typeof ViewOrganizationSchema>;

export const ViewProjectSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  created_utc: iso.datetime(),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  organization_name: string().min(1, "Organization name is required"),
  name: string().min(1, "Name is required"),
  delete_bot_data_enabled: boolean(),
  data: record(string(), unknown()).nullable().optional(),
  promoted_tabs: array(string()),
  is_configured: boolean().nullable().optional(),
  stack_count: int(),
  event_count: int(),
  has_premium_features: boolean(),
  has_slack_integration: boolean(),
  usage_hours: array(lazy(() => UsageHourInfoSchema)),
  usage: array(lazy(() => UsageInfoSchema)),
});
export type ViewProjectFormData = Infer<typeof ViewProjectSchema>;

export const ViewSavedViewSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  user_id: string()
    .length(24, "User id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "User id has invalid format")
    .nullable()
    .optional(),
  created_by_user_id: string()
    .length(24, "Created by user id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Created by user id has invalid format"),
  updated_by_user_id: string()
    .length(24, "Updated by user id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Updated by user id has invalid format")
    .nullable()
    .optional(),
  filter: string().min(1, "Filter is required").nullable().optional(),
  filter_definitions: string()
    .min(1, "Filter definitions is required")
    .nullable()
    .optional(),
  columns: record(string(), boolean()).nullable().optional(),
  column_order: array(string()).nullable().optional(),
  show_stats: boolean().nullable().optional(),
  show_chart: boolean().nullable().optional(),
  name: string().min(1, "Name is required"),
  slug: string().min(1, "Slug is required"),
  time: string().min(1, "Time is required").nullable().optional(),
  sort: string().min(1, "Sort is required").nullable().optional(),
  version: int32(),
  uses_premium_features: boolean(),
  view_type: string().min(1, "View type is required"),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type ViewSavedViewFormData = Infer<typeof ViewSavedViewSchema>;

export const ViewTokenSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  user_id: string()
    .length(24, "User id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "User id has invalid format")
    .nullable()
    .optional(),
  default_project_id: string()
    .length(24, "Default project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Default project id has invalid format")
    .nullable()
    .optional(),
  scopes: array(string()),
  expires_utc: iso.datetime().nullable().optional(),
  notes: string().min(1, "Notes is required").nullable().optional(),
  is_disabled: boolean(),
  is_suspended: boolean(),
  created_utc: iso.datetime(),
  updated_utc: iso.datetime(),
});
export type ViewTokenFormData = Infer<typeof ViewTokenSchema>;

export const ViewUserSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_ids: array(string()),
  full_name: string().min(1, "Full name is required"),
  email_address: email(),
  avatar_url: url().nullable().optional(),
  email_notifications_enabled: boolean(),
  is_email_address_verified: boolean(),
  is_active: boolean(),
  is_invite: boolean(),
  roles: array(string()),
});
export type ViewUserFormData = Infer<typeof ViewUserSchema>;

export const WebHookSchema = object({
  id: string()
    .length(24, "Id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Id has invalid format"),
  organization_id: string()
    .length(24, "Organization id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Organization id has invalid format"),
  project_id: string()
    .length(24, "Project id must be exactly 24 characters")
    .regex(/^[a-fA-F0-9]{24}$/, "Project id has invalid format"),
  url: url(),
  event_types: array(
    zodEnum([
      "CriticalError",
      "CriticalEvent",
      "NewError",
      "NewEvent",
      "StackPromoted",
      "StackRegression",
    ]),
  ),
  is_enabled: boolean(),
  version: string(),
  created_utc: iso.datetime(),
});
export type WebHookFormData = Infer<typeof WebHookSchema>;

export const WorkInProgressResultSchema = object({
  workers: array(string()),
});
export type WorkInProgressResultFormData = Infer<
  typeof WorkInProgressResultSchema
>;
