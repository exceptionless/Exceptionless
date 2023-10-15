interface Document {
	addEventListener(type: string, listener: (this: Document, ev: CustomEvent) => void): void;
	removeEventListener(type: string, listener: (this: Document, ev: CustomEvent) => void): void;
}
