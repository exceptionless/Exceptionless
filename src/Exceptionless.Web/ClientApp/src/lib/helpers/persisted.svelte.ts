export abstract class Serializer<T> {
    abstract deserialize(item: string): T;
    abstract serialize(value: T): string;
}

export class JSONSerializer<T> extends Serializer<T> {
    deserialize(item: string): T {
        return JSON.parse(item);
    }
    serialize(value: T): string {
        return JSON.stringify(value);
    }
}

export class LocalStore<T> {
    value = $state<T>() as T;
    key = '';
    serializer: Serializer<T>;

    constructor(key: string, defaultValue: T, serializer: Serializer<T>) {
        this.key = key;
        this.value = defaultValue;
        this.serializer = serializer;

        const item = localStorage.getItem(key);
        if (item !== null) {
            this.value = this.serializer.deserialize(item);
        } else {
            this.value = defaultValue;
        }

        $effect.root(() => {
            $effect(() => {
                if (this.value === undefined || this.value === null) {
                    localStorage.removeItem(this.key);
                } else {
                    localStorage.setItem(this.key, this.serializer.serialize(this.value));
                }
            });
        });
    }
}

export function persisted<T>(key: string, defaultValue: T, serializer: Serializer<T> = new JSONSerializer<T>()) {
    return new LocalStore(key, defaultValue, serializer);
}
