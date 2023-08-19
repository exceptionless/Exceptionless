/* eslint-disable */
/* tslint:disable */
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export interface BillingPlan {
	id?: string | null;
	name?: string | null;
	description?: string | null;
	/** @format double */
	price?: number;
	/** @format int32 */
	max_projects?: number;
	/** @format int32 */
	max_users?: number;
	/** @format int32 */
	retention_days?: number;
	/** @format int32 */
	max_events_per_month?: number;
	has_premium_features?: boolean;
	is_hidden?: boolean;
}

/**
 *
 *
 * 0 = Trialing
 *
 * 1 = Active
 *
 * 2 = PastDue
 *
 * 3 = Canceled
 *
 * 4 = Unpaid
 * @format int32
 */
export enum BillingStatus {
	Trialing = 0,
	Active = 1,
	PastDue = 2,
	Canceled = 3,
	Unpaid = 4
}

export interface ChangePasswordModel {
	current_password?: string | null;
	password?: string | null;
}

export interface ChangePlanResult {
	success?: boolean;
	message?: string | null;
}

export interface ClientConfiguration {
	/** @format int32 */
	version?: number;
	settings?: Record<string, string>;
}

export interface CountResult {
	/** @format int64 */
	total?: number;
	aggregations?: Record<string, IAggregate>;
	data?: Record<string, any>;
}

export interface IAggregate {
	data?: Record<string, any>;
}

export interface Invite {
	token?: string | null;
	email_address?: string | null;
	/** @format date-time */
	date_added?: string;
}

export interface Invoice {
	id?: string | null;
	organization_id?: string | null;
	organization_name?: string | null;
	/** @format date-time */
	date?: string;
	paid?: boolean;
	/** @format double */
	total?: number;
	items?: InvoiceLineItem[] | null;
}

export interface InvoiceGridModel {
	id?: string | null;
	/** @format date-time */
	date?: string;
	paid?: boolean;
}

export interface InvoiceLineItem {
	description?: string | null;
	date?: string | null;
	/** @format double */
	amount?: number;
}

export interface LoginModel {
	email?: string | null;
	password?: string | null;
	invite_token?: string | null;
}

export interface NewOrganization {
	name?: string | null;
}

export interface NewProject {
	organization_id?: string | null;
	name?: string | null;
	delete_bot_data_enabled?: boolean;
}

export interface NewToken {
	organization_id?: string | null;
	project_id?: string | null;
	default_project_id?: string | null;
	/** @uniqueItems true */
	scopes?: string[] | null;
	/** @format date-time */
	expires_utc?: string | null;
	notes?: string | null;
}

export interface NewWebHook {
	organization_id?: string | null;
	project_id?: string | null;
	url?: string | null;
	event_types?: string[] | null;
	/** The schema version that should be used. */
	version?: string | null;
}

export interface NotificationSettings {
	send_daily_summary?: boolean;
	report_new_errors?: boolean;
	report_critical_errors?: boolean;
	report_event_regressions?: boolean;
	report_new_events?: boolean;
	report_critical_events?: boolean;
}

export interface OAuthAccount {
	provider?: string | null;
	provider_user_id?: string | null;
	username?: string | null;
	extra_data?: Record<string, string>;
}

export interface PersistentEvent {
	id?: string | null;
	organization_id?: string | null;
	project_id?: string | null;
	stack_id?: string | null;
	is_first_occurrence?: boolean;
	/** @format date-time */
	created_utc?: string;
	idx?: Record<string, any>;
	type?: string | null;
	source?: string | null;
	/** @format date-time */
	date?: string;
	/** @uniqueItems true */
	tags?: string[] | null;
	message?: string | null;
	geo?: string | null;
	/** @format double */
	value?: number | null;
	/** @format int32 */
	count?: number | null;
	data?: Record<string, any>;
	reference_id?: string | null;
}

export interface ResetPasswordModel {
	password_reset_token?: string | null;
	password?: string | null;
}

export interface SignupModel {
	name?: string | null;
	email?: string | null;
	password?: string | null;
	invite_token?: string | null;
}

export interface Stack {
	id?: string | null;
	organization_id?: string | null;
	project_id?: string | null;
	type?: string | null;
	/**
	 *
	 *
	 * open
	 *
	 * fixed
	 *
	 * regressed
	 *
	 * snoozed
	 *
	 * ignored
	 *
	 * discarded
	 */
	status?: StackStatus;
	/** @format date-time */
	snooze_until_utc?: string | null;
	signature_hash?: string | null;
	signature_info?: Record<string, string>;
	fixed_in_version?: string | null;
	/** @format date-time */
	date_fixed?: string | null;
	title?: string | null;
	/** @format int32 */
	total_occurrences?: number;
	/** @format date-time */
	first_occurrence?: string;
	/** @format date-time */
	last_occurrence?: string;
	description?: string | null;
	occurrences_are_critical?: boolean;
	references?: string[] | null;
	/** @uniqueItems true */
	tags?: string[] | null;
	duplicate_signature?: string | null;
	/** @format date-time */
	created_utc?: string;
	/** @format date-time */
	updated_utc?: string;
	is_deleted?: boolean;
	allow_notifications?: boolean;
}

/**
 *
 *
 * open
 *
 * fixed
 *
 * regressed
 *
 * snoozed
 *
 * ignored
 *
 * discarded
 */
export enum StackStatus {
	Open = 'open',
	Fixed = 'fixed',
	Regressed = 'regressed',
	Snoozed = 'snoozed',
	Ignored = 'ignored',
	Discarded = 'discarded'
}

export interface StringStringValuesKeyValuePair {
	key?: string;
	value?: string[];
}

export interface StringValueFromBody {
	value?: string | null;
}

export interface TokenResult {
	token?: string | null;
}

export interface UpdateEmailAddressResult {
	is_verified?: boolean;
}

export interface UsageHourInfo {
	/** @format date-time */
	date?: string;
	/** @format int32 */
	total?: number;
	/** @format int32 */
	blocked?: number;
	/** @format int32 */
	discarded?: number;
	/** @format int32 */
	too_big?: number;
}

export interface UsageInfo {
	/** @format date-time */
	date?: string;
	/** @format int32 */
	limit?: number;
	/** @format int32 */
	total?: number;
	/** @format int32 */
	blocked?: number;
	/** @format int32 */
	discarded?: number;
	/** @format int32 */
	too_big?: number;
}

export interface User {
	id?: string | null;
	organization_ids?: string[] | null;
	password?: string | null;
	salt?: string | null;
	password_reset_token?: string | null;
	/** @format date-time */
	password_reset_token_expiration?: string;
	o_auth_accounts?: OAuthAccount[] | null;
	full_name?: string | null;
	email_address?: string | null;
	email_notifications_enabled?: boolean;
	is_email_address_verified?: boolean;
	verify_email_address_token?: string | null;
	/** @format date-time */
	verify_email_address_token_expiration?: string;
	is_active?: boolean;
	roles?: string[] | null;
	/** @format date-time */
	created_utc?: string;
	/** @format date-time */
	updated_utc?: string;
}

export interface UserDescription {
	email_address?: string | null;
	description?: string | null;
	data?: Record<string, any>;
}

export interface ViewOrganization {
	id?: string | null;
	/** @format date-time */
	created_utc?: string;
	name?: string | null;
	plan_id?: string | null;
	plan_name?: string | null;
	plan_description?: string | null;
	card_last4?: string | null;
	/** @format date-time */
	subscribe_date?: string | null;
	/** @format date-time */
	billing_change_date?: string | null;
	billing_changed_by_user_id?: string | null;
	/**
	 *
	 *
	 * 0 = Trialing
	 *
	 * 1 = Active
	 *
	 * 2 = PastDue
	 *
	 * 3 = Canceled
	 *
	 * 4 = Unpaid
	 */
	billing_status?: BillingStatus;
	/** @format double */
	billing_price?: number;
	/** @format int32 */
	max_events_per_month?: number;
	/** @format int32 */
	bonus_events_per_month?: number;
	/** @format date-time */
	bonus_expiration?: string | null;
	/** @format int32 */
	retention_days?: number;
	is_suspended?: boolean;
	suspension_code?: string | null;
	suspension_notes?: string | null;
	/** @format date-time */
	suspension_date?: string | null;
	has_premium_features?: boolean;
	/** @format int32 */
	max_users?: number;
	/** @format int32 */
	max_projects?: number;
	/** @format int64 */
	project_count?: number;
	/** @format int64 */
	stack_count?: number;
	/** @format int64 */
	event_count?: number;
	invites?: Invite[] | null;
	usage_hours?: UsageHourInfo[] | null;
	usage?: UsageInfo[] | null;
	data?: Record<string, any>;
	is_throttled?: boolean;
	is_over_monthly_limit?: boolean;
	is_over_request_limit?: boolean;
}

export interface ViewProject {
	id?: string | null;
	/** @format date-time */
	created_utc?: string;
	organization_id?: string | null;
	organization_name?: string | null;
	name?: string | null;
	delete_bot_data_enabled?: boolean;
	data?: Record<string, any>;
	/** @uniqueItems true */
	promoted_tabs?: string[] | null;
	is_configured?: boolean | null;
	/** @format int64 */
	stack_count?: number;
	/** @format int64 */
	event_count?: number;
	has_premium_features?: boolean;
	has_slack_integration?: boolean;
	usage_hours?: UsageHourInfo[] | null;
	usage?: UsageInfo[] | null;
}

export interface ViewToken {
	id?: string | null;
	organization_id?: string | null;
	project_id?: string | null;
	user_id?: string | null;
	default_project_id?: string | null;
	/** @uniqueItems true */
	scopes?: string[] | null;
	/** @format date-time */
	expires_utc?: string | null;
	notes?: string | null;
	is_disabled?: boolean;
	is_suspended?: boolean;
	/** @format date-time */
	created_utc?: string;
	/** @format date-time */
	updated_utc?: string;
}

export interface ViewUser {
	id?: string | null;
	organization_ids?: string[] | null;
	full_name?: string | null;
	email_address?: string | null;
	email_notifications_enabled?: boolean;
	is_email_address_verified?: boolean;
	is_active?: boolean;
	is_invite?: boolean;
	roles?: string[] | null;
}

export interface WebHook {
	id?: string | null;
	organization_id?: string | null;
	project_id?: string | null;
	url?: string | null;
	event_types?: string[] | null;
	is_enabled?: boolean;
	version?: string | null;
	/** @format date-time */
	created_utc?: string;
}
