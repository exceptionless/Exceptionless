export abstract class Serializer<T> {
    abstract deserialize(item: string): T;
    abstract serialize(value: T): null | string;
}

export class AuthJSONSerializer extends Serializer<null | string> {
    deserialize(item: string): null | string {
        if (item === 'null') {
            return null;
        }

        return item;
    }
    serialize(value: null | string): null | string {
        return value;
    }
}

export class JSONSerializer<T> extends Serializer<T> {
    deserialize(item: string): T {
        return JSON.parse(item);
    }
    serialize(value: T): null | string {
        return JSON.stringify(value);
    }
}

export class LocalStore<T> {
    private _value = $state<T>() as T;

    constructor(
        public key: string,
        defaultValue: T,
        public serializer: Serializer<T>
    ) {
        this._value = defaultValue;

        const item = localStorage.getItem(key);
        if (item !== null) {
            this._value = this.serializer.deserialize(item);
        } else {
            this._value = defaultValue;
        }

        $effect.root(() => {
            $effect(() => {
                if (this._value === undefined || this._value === null) {
                    localStorage.removeItem(this.key);
                } else {
                    localStorage.setItem(this.key, this.serializer.serialize(this._value));
                }
            });
        });
    }

    public get value(): T {
        return this._value;
    }

    public set value(value: T) {
        this._value = value;
    }
}

export function persisted<T>(key: string, defaultValue: T, serializer: Serializer<T> = new JSONSerializer<T>()) {
    return new LocalStore(key, defaultValue, serializer);
}
