export interface MutexEntry {
    owner: any;
    resolve: (release: ReleaseFunction) => void;
}

export class Mutex {
    private _linkedList = new LinkedList<MutexEntry>();
    private _isLocked = false;
    private _currentOwner: any = null;

    acquire(owner: any) {
        return new Promise<ReleaseFunction>((resolve) => {
            this._linkedList.append({ owner, resolve });
            this._dispatch();
        });
    }

    async runExclusive<T>(owner: any, callback: () => Promise<T>) {
        if (this._currentOwner === owner) {
            // If the current owner is the same, execute the event immediately.
            return callback();
        }

        const release = await this.acquire(owner);

        try {
            this._currentOwner = owner;
            return await callback();
        } finally {
            this._currentOwner = null;
            release();
        }
    }

    private _dispatch() {
        if (this._isLocked) {
            return;
        }

        const nextEntry = this._linkedList.popFront();

        if (!nextEntry) {
            return;
        }

        this._isLocked = true;
        this._currentOwner = nextEntry.owner;
        nextEntry.resolve(this._buildRelease());
    }

    private _buildRelease(): ReleaseFunction {
        return () => {
            this._isLocked = false;
            this._currentOwner = null;
            this._dispatch();
        };
    }
}

type ReleaseFunction = () => void;

class Node<T> {
    data: () => any;
    next: Node<T> | null;

    constructor(data: any) {
        this.data = data;
        this.next = null;
    }
}

class LinkedList<T> {
    head: Node<T> | null | undefined;
    tail: Node<T> | null | undefined;

    constructor() {
        this.head = null;
        this.tail = null;
    }

    isEmpty(): boolean {
        return this.head === null;
    }

    append(data: T): void {
        const newNode = new Node<T>(data);

        if (this.isEmpty()) {
            this.head = newNode;
            this.tail = newNode;
        } else {
            if (this.tail) {
                this.tail.next = newNode;
                this.tail = newNode;
            }
        }
    }

    prepend(data: T): void {
        const newNode = new Node<T>(data);

        if (this.isEmpty()) {
            this.head = newNode;
            this.tail = newNode;
        } else {
            if (this.head) {
                newNode.next = this.head;
                this.head = newNode;
            }
        }
    }

    popFront(): any | undefined {
        if (this.isEmpty()) {
            return undefined;
        }

        const poppedData = this.head?.data;
        this.head = this.head?.next;

        if (!this.head) {
            this.tail = null;
        }

        return poppedData;
    }

    toArray(): any[] {
        const result: any[] = [];
        let currentNode = this.head;

        while (currentNode) {
            result.push(currentNode.data);
            currentNode = currentNode.next;
        }

        return result;
    }
}
