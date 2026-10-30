declare interface PriorityQueueElement {
    priority: Priority;
    value: () => any;
}

export class PriorityQueue {
    private readonly logPrefix: string;
    private elements: PriorityQueueElement[] = [];
    private enqueueLock: Promise<void> = Promise.resolve();
    private dequeueLock: Promise<void> = Promise.resolve();

    constructor(logPrefix: string) {
        this.logPrefix = logPrefix;
    }

    /**
     * Adds an element to the queue with the specified priority.
     * @param value A function that returns the value of the element.
     * @param priority The priority of the element.
     * @throws An error if the value argument is not a function.
     */
    enqueue(value: () => any, priority: number): void {
        // handle errors in the value function
        if (typeof value !== "function") {
            const errorMsg = `PriorityQueue:: ${this.logPrefix}: Error enqueuing event: value is not a function`;
            throw new Error(errorMsg);
        }

        const element: PriorityQueueElement = { value, priority };

        this.enqueueLock = this.enqueueLock.then(() => {
            // add the element to the binary heap
            this.elements.push(element);
            this.upheap(this.elements.length - 1);

            // use a microtask to ensure that only one callback is processed at a time
            Promise.resolve().then(() => {
                this.processRequest();
            });
        });
    }

    /**
     * Removes and returns the element with the highest priority from the queue.
     * @returns A Promise that resolves with the value of the element, or undefined if the queue is empty.
     */
    dequeue(): Promise<(() => any) | undefined> {
        // return the dequeueLock promise, which ensures that only one instance of the dequeue method runs at a time
        return this.dequeueLock.then(() => {
            return new Promise<(() => any) | undefined>((resolve) => {
                // remove the root element from the binary heap
                const root = this.elements[0];
                const last = this.elements.pop();

                if (this.elements.length > 0) {
                    if (last) {
                        this.elements[0] = last;
                    }

                    this.downheap(0);
                }

                if (root) {
                    resolve(root.value);
                } else {
                    resolve(undefined);
                }
            });
        });
    }

    /**
     * Returns true if the queue is empty, false otherwise.
     */
    isEmpty(): boolean {
        return this.elements.length === 0;
    }

    /**
     * Returns the value of the element with the highest priority, or undefined if the queue is empty.
     */
    peek(): (() => any) | undefined {
        return this.elements[0]?.value;
    }

    /**
     * Returns the number of elements in the queue.
     */
    size(): number {
        return this.elements.length;
    }

    private upheap(index: number) {
        if (index === 0) {
            return;
        }

        const parentIndex = Math.floor((index - 1) / 2);

        if (this.elements[index].priority < this.elements[parentIndex].priority) {
            // swap the elements and upheap again
            const temp = this.elements[parentIndex];
            this.elements[parentIndex] = this.elements[index];
            this.elements[index] = temp;

            this.upheap(parentIndex);
        }
    }

    private downheap(index: number) {
        const leftIndex = 2 * index + 1;
        const rightIndex = 2 * index + 2;
        let smallest = index;

        if (leftIndex < this.elements.length && this.elements[leftIndex].priority < this.elements[smallest].priority) {
            smallest = leftIndex;
        }

        if (rightIndex < this.elements.length && this.elements[rightIndex].priority < this.elements[smallest].priority) {
            smallest = rightIndex;
        }

        if (smallest !== index) {
            // swap the elements and downheap again
            const temp = this.elements[index];
            this.elements[index] = this.elements[smallest];
            this.elements[smallest] = temp;

            this.downheap(smallest);
        }
    }

    private async processRequest() {
        if (this.size() > 0) {
            const callback = await this.dequeue();

            if (callback) {
                try {
                    await callback();
                } catch (error) {
                    const errorMsg = `PriorityQueue:: ${this.logPrefix}: Error processing event: ${(error as Error).message}`;
                    throw new Error(errorMsg);
                }
            } else {
                return;
            }
        }

        return;
    }
}

export enum Priority {
    High = 0,
    Medium = 1,
    Low = 2
}
