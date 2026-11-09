
/** String encoding utility constants */
export function encode(text: string): string {
    return btoa(text);
}
export function decode(encodedText: string): string {
    return atob(encodedText);
}

export class Guid {
    static newGuid() {
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0,
                // tslint:disable-next-line:triple-equals
                v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
}