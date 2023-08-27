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

import { IsDate, IsEmail } from 'class-validator';

export class BillingPlan {
	id?: string;
	name?: string;
	description?: string;
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

export class ChangePasswordModel {
	current_password?: string | null;
	password?: string | null;
}

export class ChangePlanResult {
	success?: boolean;
	message?: string | null;
}

export class ClientConfiguration {
	/** @format int32 */
	version?: number;
	settings?: Record<string, string>;
}

export class CountResult {
	/** @format int64 */
	total?: number;
	aggregations?: Record<string, IAggregate>;
	data?: Record<string, unknown>;
}

export class IAggregate {
	data?: Record<string, unknown>;
}

export class Invite {
	token?: string;
	email_address?: string;
	/** @format date-time */
	@IsDate() date_added?: string;
}

export class Invoice {
	id?: string;
	organization_id?: string;
	organization_name?: string;
	/** @format date-time */
	@IsDate() date?: string;
	paid?: boolean;
	/** @format double */
	total?: number;
	items?: InvoiceLineItem[];
}

export class InvoiceGridModel {
	id?: string;
	/** @format date-time */
	@IsDate() date?: string;
	paid?: boolean;
}

export class InvoiceLineItem {
	description?: string;
	date?: string | null;
	/** @format double */
	amount?: number;
}

export class LoginModel {
	constructor(email: string, password: string) {
		this.email = email;
		this.password = password;
	}

	/** @format email */
	@IsEmail({ require_tld: false }) email: string;
	password: string;
	invite_token?: string | null;
}

export class NewOrganization {
	name?: string;
}

export class NewProject {
	organization_id?: string;
	name?: string;
	delete_bot_data_enabled?: boolean;
}

export class NewToken {
	organization_id?: string;
	project_id?: string;
	default_project_id?: string | null;
	scopes?: string[];
	/** @format date-time */
	@IsDate() expires_utc?: string | null;
	notes?: string | null;
}

export class NewWebHook {
	organization_id?: string;
	project_id?: string;
	url?: string;
	event_types?: string[];
	/** The schema version that should be used. */
	version?: string;
}

export class NotificationSettings {
	send_daily_summary?: boolean;
	report_new_errors?: boolean;
	report_critical_errors?: boolean;
	report_event_regressions?: boolean;
	report_new_events?: boolean;
	report_critical_events?: boolean;
}

export class OAuthAccount {
	provider?: string;
	provider_user_id?: string;
	username?: string;
	extra_data?: Record<string, string>;
}

export class PersistentEvent {
	id?: string;
	organization_id?: string;
	project_id?: string;
	stack_id?: string;
	is_first_occurrence?: boolean;
	/** @format date-time */
	@IsDate() created_utc?: string;
	idx?: Record<string, unknown>;
	type?: string | null;
	source?: string | null;
	/** @format date-time */
	@IsDate() date?: string;
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

export class ResetPasswordModel {
	password_reset_token?: string | null;
	password?: string | null;
}

export class SignupModel {
	constructor(email: string, password: string) {
		this.email = email;
		this.password = password;
	}

	name?: string;
	/** @format email */
	@IsEmail({ require_tld: false }) email: string;
	password: string;
	invite_token?: string | null;
}

export class Stack {
	id?: string;
	organization_id?: string;
	project_id?: string;
	type?: string;
	/**
	 *
	 * open
	 * fixed
	 * regressed
	 * snoozed
	 * ignored
	 * discarded
	 */
	status?: StackStatus;
	/** @format date-time */
	@IsDate() snooze_until_utc?: string | null;
	signature_hash?: string;
	signature_info?: Record<string, string>;
	fixed_in_version?: string | null;
	/** @format date-time */
	@IsDate() date_fixed?: string | null;
	title?: string;
	/** @format int32 */
	total_occurrences?: number;
	/** @format date-time */
	@IsDate() first_occurrence?: string;
	/** @format date-time */
	@IsDate() last_occurrence?: string;
	description?: string | null;
	occurrences_are_critical?: boolean;
	references?: string[];
	tags?: string[];
	duplicate_signature?: string;
	/** @format date-time */
	@IsDate() created_utc?: string;
	/** @format date-time */
	@IsDate() updated_utc?: string;
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

export class StringStringValuesKeyValuePair {
	key?: string;
	value?: string[];
}

export class StringValueFromBody {
	value?: string;
}

export class TokenResult {
	token?: string;
}

export class UpdateEmailAddressResult {
	is_verified?: boolean;
}

export class UsageHourInfo {
	/** @format date-time */
	@IsDate() date?: string;
	/** @format int32 */
	total?: number;
	/** @format int32 */
	blocked?: number;
	/** @format int32 */
	discarded?: number;
	/** @format int32 */
	too_big?: number;
}

export class UsageInfo {
	/** @format date-time */
	@IsDate() date?: string;
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

export class User {
	constructor(full_name: string, email_address: string) {
		this.full_name = full_name;
		this.email_address = email_address;
	}

	id?: string;
	organization_ids?: string[];
	password?: string | null;
	salt?: string | null;
	password_reset_token?: string | null;
	/** @format date-time */
	@IsDate() password_reset_token_expiration?: string;
	o_auth_accounts?: OAuthAccount[];
	full_name: string;
	/** @format email */
	@IsEmail({ require_tld: false }) email_address: string;
	email_notifications_enabled?: boolean;
	is_email_address_verified?: boolean;
	verify_email_address_token?: string | null;
	/** @format date-time */
	@IsDate() verify_email_address_token_expiration?: string;
	is_active?: boolean;
	roles?: string[];
	/** @format date-time */
	@IsDate() created_utc?: string;
	/** @format date-time */
	@IsDate() updated_utc?: string;
}

export class UserDescription {
	constructor(description: string) {
		this.description = description;
	}

	email_address?: string | null;
	description: string;
	data?: Record<string, unknown>;
}

export class ViewOrganization {
	id?: string;
	/** @format date-time */
	@IsDate() created_utc?: string;
	name?: string;
	plan_id?: string;
	plan_name?: string;
	plan_description?: string;
	'card_last4'?: string | null;
	/** @format date-time */
	@IsDate() subscribe_date?: string | null;
	/** @format date-time */
	@IsDate() billing_change_date?: string | null;
	billing_changed_by_user_id?: string | null;
	/**
	 *
	 * 0 = Trialing
	 * 1 = Active
	 * 2 = PastDue
	 * 3 = Canceled
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
	@IsDate() bonus_expiration?: string | null;
	/** @format int32 */
	retention_days?: number;
	is_suspended?: boolean;
	suspension_code?: string | null;
	suspension_notes?: string | null;
	/** @format date-time */
	@IsDate() suspension_date?: string | null;
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
	invites?: Invite[];
	usage_hours?: UsageHourInfo[];
	usage?: UsageInfo[];
	data?: Record<string, unknown>;
	is_throttled?: boolean;
	is_over_monthly_limit?: boolean;
	is_over_request_limit?: boolean;
}

export class ViewProject {
	id?: string;
	/** @format date-time */
	@IsDate() created_utc?: string;
	organization_id?: string;
	organization_name?: string;
	name?: string;
	delete_bot_data_enabled?: boolean;
	data?: Record<string, unknown>;
	promoted_tabs?: string[];
	is_configured?: boolean | null;
	/** @format int64 */
	stack_count?: number;
	/** @format int64 */
	event_count?: number;
	has_premium_features?: boolean;
	has_slack_integration?: boolean;
	usage_hours?: UsageHourInfo[];
	usage?: UsageInfo[];
}

export class ViewToken {
	id?: string;
	organization_id?: string;
	project_id?: string;
	user_id?: string | null;
	default_project_id?: string | null;
	scopes?: string[];
	/** @format date-time */
	@IsDate() expires_utc?: string | null;
	notes?: string | null;
	is_disabled?: boolean;
	is_suspended?: boolean;
	/** @format date-time */
	@IsDate() created_utc?: string;
	/** @format date-time */
	@IsDate() updated_utc?: string;
}

export class ViewUser {
	id?: string;
	organization_ids?: string[];
	full_name?: string;
	email_address?: string;
	email_notifications_enabled?: boolean;
	is_email_address_verified?: boolean;
	is_active?: boolean;
	is_invite?: boolean;
	roles?: string[];
}

export class WebHook {
	constructor(url: string, event_types: string[], version: string) {
		this.url = url;
		this.event_types = event_types;
		this.version = version;
	}

	id?: string;
	organization_id?: string;
	project_id?: string;
	url: string;
	event_types: string[];
	is_enabled?: boolean;
	version: string;
	/** @format date-time */
	@IsDate() created_utc?: string;
}
