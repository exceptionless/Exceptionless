export enum StackStatus {
  Open = "open",
  Fixed = "fixed",
  Regressed = "regressed",
  Snoozed = "snoozed",
  Ignored = "ignored",
  Discarded = "discarded",
}

/** @format int32 */
export enum BillingStatus {
  Trialing = 0,
  Active = 1,
  PastDue = 2,
  Canceled = 3,
  Unpaid = 4,
}

export interface BillingPlan {
  id: string;
  name: string;
  description: string;
  /** @format double */
  price: number;
  /** @format int32 */
  max_projects: number;
  /** @format int32 */
  max_users: number;
  /** @format int32 */
  retention_days: number;
  /** @format int32 */
  max_events_per_month: number;
  has_premium_features: boolean;
  is_hidden: boolean;
}

export interface ChangePasswordModel {
  current_password: string;
  password: string;
}

export interface ChangePlanResult {
  success: boolean;
  message?: string | null;
}

export interface ClientConfiguration {
  /** @format int32 */
  version: number;
  settings: Record<string, string>;
}

export interface CountResult {
  /** @format int64 */
  total: number;
  aggregations?: Record<string, IAggregate>;
  data?: Record<string, unknown>;
}

export interface ExternalAuthInfo {
  clientId: string;
  code: string;
  redirectUri: string;
  inviteToken?: string | null;
}

export interface IAggregate {
  data?: Record<string, unknown>;
}

export interface Invite {
  token: string;
  email_address: string;
  /** @format date-time */
  date_added: string;
}

export interface Invoice {
  id: string;
  organization_id: string;
  organization_name: string;
  /** @format date-time */
  date: string;
  paid: boolean;
  /** @format double */
  total: number;
  items: InvoiceLineItem[];
}

export interface InvoiceGridModel {
  id: string;
  /** @format date-time */
  date: string;
  paid: boolean;
}

export interface InvoiceLineItem {
  description: string;
  date?: string | null;
  /** @format double */
  amount: number;
}

export interface Login {
  /** The email address or domain username */
  email: string;
  password: string;
  invite_token?: string | null;
}

export interface NewOrganization {
  name: string;
}

export interface NewProject {
  organization_id: string;
  name: string;
  delete_bot_data_enabled: boolean;
}

export interface NewToken {
  organization_id: string;
  project_id: string;
  default_project_id?: string | null;
  scopes: string[];
  /** @format date-time */
  expires_utc?: string | null;
  notes?: string | null;
}

export interface NewWebHook {
  organization_id: string;
  project_id: string;
  url: string;
  event_types: string[];
  /** The schema version that should be used. */
  version?: string | null;
}

export interface NotificationSettings {
  send_daily_summary: boolean;
  report_new_errors: boolean;
  report_critical_errors: boolean;
  report_event_regressions: boolean;
  report_new_events: boolean;
  report_critical_events: boolean;
}

export interface OAuthAccount {
  provider: string;
  provider_user_id: string;
  username: string;
  extra_data: Record<string, string>;
}

export interface PersistentEvent {
  id: string;
  organization_id: string;
  project_id: string;
  stack_id: string;
  is_first_occurrence: boolean;
  /** @format date-time */
  created_utc: string;
  idx: Record<string, unknown>;
  type?: string | null;
  source?: string | null;
  /** @format date-time */
  date: string;
  tags?: string[] | null;
  message?: string | null;
  geo?: string | null;
  /** @format double */
  value?: number | null;
  /** @format int32 */
  count?: number | null;
  data?: Record<string, unknown>;
  reference_id?: string | null;
}

export interface ResetPasswordModel {
  password_reset_token: string;
  password: string;
}

export interface Signup {
  name: string;
  /** The email address or domain username */
  email: string;
  password: string;
  invite_token?: string | null;
}

export interface Stack {
  id: string;
  organization_id: string;
  project_id: string;
  type: string;
  status: StackStatus;
  /** @format date-time */
  snooze_until_utc?: string | null;
  signature_hash: string;
  signature_info: Record<string, string>;
  fixed_in_version?: string | null;
  /** @format date-time */
  date_fixed?: string | null;
  title: string;
  /** @format int32 */
  total_occurrences: number;
  /** @format date-time */
  first_occurrence: string;
  /** @format date-time */
  last_occurrence: string;
  description?: string | null;
  occurrences_are_critical: boolean;
  references: string[];
  tags: string[];
  duplicate_signature: string;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
  is_deleted: boolean;
  allow_notifications: boolean;
}

export interface StringStringValuesKeyValuePair {
  key?: string | null;
  value: string[];
}

export interface StringValueFromBody {
  value?: string | null;
}

export interface TokenResult {
  token: string;
}

export interface UpdateEmailAddressResult {
  is_verified: boolean;
}

export interface UsageHourInfo {
  /** @format date-time */
  date: string;
  /** @format int32 */
  total: number;
  /** @format int32 */
  blocked: number;
  /** @format int32 */
  discarded: number;
  /** @format int32 */
  too_big: number;
}

export interface UsageInfo {
  /** @format date-time */
  date: string;
  /** @format int32 */
  limit: number;
  /** @format int32 */
  total: number;
  /** @format int32 */
  blocked: number;
  /** @format int32 */
  discarded: number;
  /** @format int32 */
  too_big: number;
}

export interface User {
  id: string;
  organization_ids: string[];
  password?: string | null;
  salt?: string | null;
  password_reset_token?: string | null;
  /** @format date-time */
  password_reset_token_expiration: string;
  o_auth_accounts: OAuthAccount[];
  full_name: string;
  /** @format email */
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  verify_email_address_token?: string | null;
  /** @format date-time */
  verify_email_address_token_expiration: string;
  is_active: boolean;
  roles: string[];
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
}

export interface UserDescription {
  email_address?: string | null;
  description?: string | null;
  data?: Record<string, unknown>;
}

export interface ViewCurrentUser {
  hash?: string | null;
  has_local_account: boolean;
  o_auth_accounts: OAuthAccount[];
  id: string;
  organization_ids: string[];
  full_name: string;
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  is_active: boolean;
  is_invite: boolean;
  roles: string[];
}

export interface ViewOrganization {
  id: string;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
  name: string;
  plan_id: string;
  plan_name: string;
  plan_description: string;
  card_last4?: string | null;
  /** @format date-time */
  subscribe_date?: string | null;
  /** @format date-time */
  billing_change_date?: string | null;
  billing_changed_by_user_id?: string | null;
  billing_status: BillingStatus;
  /** @format double */
  billing_price: number;
  /** @format int32 */
  max_events_per_month: number;
  /** @format int32 */
  bonus_events_per_month: number;
  /** @format date-time */
  bonus_expiration?: string | null;
  /** @format int32 */
  retention_days: number;
  is_suspended: boolean;
  suspension_code?: string | null;
  suspension_notes?: string | null;
  /** @format date-time */
  suspension_date?: string | null;
  has_premium_features: boolean;
  /** @format int32 */
  max_users: number;
  /** @format int32 */
  max_projects: number;
  /** @format int64 */
  project_count: number;
  /** @format int64 */
  stack_count: number;
  /** @format int64 */
  event_count: number;
  invites: Invite[];
  usage_hours: UsageHourInfo[];
  usage: UsageInfo[];
  data?: Record<string, unknown>;
  is_throttled: boolean;
  is_over_monthly_limit: boolean;
  is_over_request_limit: boolean;
}

export interface ViewProject {
  id: string;
  /** @format date-time */
  created_utc: string;
  organization_id: string;
  organization_name: string;
  name: string;
  delete_bot_data_enabled: boolean;
  data?: Record<string, unknown>;
  promoted_tabs: string[];
  is_configured?: boolean | null;
  /** @format int64 */
  stack_count: number;
  /** @format int64 */
  event_count: number;
  has_premium_features: boolean;
  has_slack_integration: boolean;
  usage_hours: UsageHourInfo[];
  usage: UsageInfo[];
}

export interface ViewToken {
  id: string;
  organization_id: string;
  project_id: string;
  user_id?: string | null;
  default_project_id?: string | null;
  scopes: string[];
  /** @format date-time */
  expires_utc?: string | null;
  notes?: string | null;
  is_disabled: boolean;
  is_suspended: boolean;
  /** @format date-time */
  created_utc: string;
  /** @format date-time */
  updated_utc: string;
}

export interface ViewUser {
  id: string;
  organization_ids: string[];
  full_name: string;
  email_address: string;
  email_notifications_enabled: boolean;
  is_email_address_verified: boolean;
  is_active: boolean;
  is_invite: boolean;
  roles: string[];
}

export interface WebHook {
  id: string;
  organization_id: string;
  project_id: string;
  url: string;
  event_types: string[];
  is_enabled: boolean;
  version: string;
  /** @format date-time */
  created_utc: string;
}

export interface WorkInProgressResult {
  workers: string[];
}
