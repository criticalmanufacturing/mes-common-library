export async function sleep(ms?: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export async function waitFor(timeout: number, errorMessage: string, callback: any): Promise<void> {
    while (true) {
        if (callback()) { return; }
        if (timeout <= 0) { throw Error(errorMessage); }
        timeout -= 100;
        await sleep(100);
    }
}
