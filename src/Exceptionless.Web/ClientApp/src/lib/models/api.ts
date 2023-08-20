import { IsEmail, MinLength } from 'class-validator';

import type { LoginModel } from './api.generated';

export class Login implements LoginModel {
	constructor(inviteToken?: string | null) {
		if (inviteToken) {
			this.invite_token = inviteToken;
		}
	}

	@IsEmail({ require_tld: false })
	email!: string;

	@MinLength(6)
	password!: string;

	invite_token?: string;
}
