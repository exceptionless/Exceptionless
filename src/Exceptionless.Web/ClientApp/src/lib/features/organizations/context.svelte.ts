import { PersistedState } from 'runed';

export const organization = new PersistedState<string | undefined>('organization', undefined);

class ShowOrganizationNotificationsState {
    public get current() {
        return this._visible;
    }

    private _visible = $state(true);

    public set(value: boolean) {
        this._visible = value;
    }
}

export const showOrganizationNotifications = new ShowOrganizationNotificationsState();
