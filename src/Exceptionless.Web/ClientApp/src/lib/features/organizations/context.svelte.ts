import { PersistedState } from 'runed';

class OrganizationState {
    get current(): string | undefined {
        return this.#persisted.current || undefined;
    }

    set current(value: string | undefined) {
        this.#persisted.current = value ?? '';
    }

    #persisted = new PersistedState<string>('organization', '');
}

export const organization = new OrganizationState();

class ShowOrganizationNotificationsState {
    get current() {
        return this._visible;
    }

    private _visible = $state(true);

    set(value: boolean) {
        this._visible = value;
    }
}

export const showOrganizationNotifications = new ShowOrganizationNotificationsState();
