import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";

import {
    CustomTimerTask,
    CustomTimerSettings,
    SETTINGS_DEFAULTS
} from "../../../../src/tasks/customTimer/customTimer.task";
import { CustomTimerType, CustomTimerWorkingMode } from "../../../../src/types/types";

describe("CustomTimer Task tests", () => {

    const createTask = async (settings: Partial<CustomTimerSettings>): Promise<CustomTimerTask> => {
        const task = new CustomTimerTask();
        Object.assign(task, SETTINGS_DEFAULTS, settings);
        (<any>task)._logger = {
            info: () => undefined,
            warning: () => undefined,
            error: () => undefined
        };
        await task.onInit();
        return task;
    };

    const activate = async (task: CustomTimerTask): Promise<void> => {
        await task.onChanges(<Task.Changes>{
            activate: { previousValue: undefined, currentValue: true }
        });
    };

    it("should emit success and a timestamp after a sleep interval", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.Sleep,
            interval: 10
        });
        const timestampPromise = new Promise<Date>((resolve) => task.timestamp.subscribe(resolve));
        const successPromise = new Promise<boolean>((resolve) => task.success.subscribe(resolve));

        await activate(task);

        chai.expect(await timestampPromise).to.be.instanceOf(Date);
        chai.expect(await successPromise).to.equal(true);
        await task.onDestroy();
    });

    it("should emit an error when a timeout interval elapses", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.Timeout,
            interval: 10
        });
        const timestampPromise = new Promise<Date>((resolve) => task.timestamp.subscribe(resolve));
        const errorPromise = new Promise<Error>((resolve) => task.error.subscribe(resolve));

        await activate(task);

        chai.expect(await timestampPromise).to.be.instanceOf(Date);
        chai.expect((await errorPromise).message).to.equal("Timeout occurred");
        await task.onDestroy();
    });

    it("should stop a timer after the configured number of occurrences", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.Timer,
            _timerWorkingMode: CustomTimerWorkingMode.NumberOfOccurrences,
            _numberOfOccurrencesAllowed: 2,
            interval: 10
        });
        let successCount = 0;
        const completionPromise = new Promise<void>((resolve) => {
            task.success.subscribe((value) => {
                chai.expect(value).to.equal(true);
                successCount++;
                if (successCount === 2) {
                    resolve();
                }
            });
        });

        await activate(task);
        await completionPromise;
        await new Promise((resolve) => setTimeout(resolve, 25));
        chai.expect(successCount).to.equal(2);
        await task.onDestroy();
    });

    it("should keep emitting while a timer is active until deactivation", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.Timer,
            _timerWorkingMode: CustomTimerWorkingMode.UntilDeactivation,
            interval: 10
        });
        let successCount = 0;
        task.success.subscribe(() => successCount++);

        await activate(task);
        await new Promise((resolve) => setTimeout(resolve, 25));
        await task.onChanges(<Task.Changes>{
            activate: { previousValue: true, currentValue: false }
        });
        const countAfterDeactivation = successCount;
        await new Promise((resolve) => setTimeout(resolve, 25));

        chai.expect(countAfterDeactivation).to.be.greaterThan(0);
        chai.expect(successCount).to.equal(countAfterDeactivation);
        await task.onDestroy();
    });

    it("should emit success when a cron job is triggered", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.CronJob,
            cronExpression: "0 * * * *"
        });
        let callback: () => void;
        let startCount = 0;
        let destroyCount = 0;
        (<any>task)._cronHandler = {
            setCronJob: (_expression: string, execute: () => void) => {
                callback = execute;
                return {
                    start: () => {
                        startCount++;
                    },
                    destroy: () => {
                        destroyCount++;
                    }
                };
            }
        };
        const successPromise = new Promise<boolean>((resolve) => task.success.subscribe(resolve));
        const timestampPromise = new Promise<Date>((resolve) => task.timestamp.subscribe(resolve));

        await activate(task);
        callback();

        chai.expect(await successPromise).to.equal(true);
        chai.expect(await timestampPromise).to.be.instanceOf(Date);
        chai.expect(startCount).to.equal(1);
        await task.onDestroy();
        chai.expect(destroyCount).to.equal(1);
    });

    it("should emit the UID for each triggered complex cron job", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.ComplexCronJob,
            cronExpressionMap: {
                first: "0 * * * *",
                second: "15 * * * *"
            }
        });
        const callbacks: Array<() => void> = [];
        const jobs: Array<{ start: () => void; destroy: () => void }> = [];
        (<any>task)._cronHandler = {
            setCronJob: (_expression: string, execute: () => void) => {
                callbacks.push(execute);
                let started = false;
                const job = {
                    start: () => {
                        if (!started) {
                            started = true;
                        }
                    },
                    destroy: () => undefined
                };
                jobs.push(job);
                return job;
            }
        };
        const uids: string[] = [];
        task.cronExpressionUID.subscribe((uid) => uids.push(uid));
        let successCount = 0;
        const completionPromise = new Promise<void>((resolve) => {
            task.success.subscribe(() => {
                successCount++;
                if (successCount === 2) {
                    resolve();
                }
            });
        });

        await activate(task);
        callbacks.forEach((cronCallback) => cronCallback());
        await completionPromise;

        chai.expect(callbacks).to.have.lengthOf(2);
        chai.expect(jobs).to.have.lengthOf(2);
        chai.expect(uids).to.deep.equal(["first", "second"]);
        await task.onDestroy();
    });

    it("should emit an error for an invalid cron expression", async () => {
        const task = await createTask({
            _autoActivate: false,
            _timerType: CustomTimerType.CronJob,
            cronExpression: "invalid cron expression"
        });
        (<any>task)._cronHandler = {
            setCronJob: () => {
                throw new Error('Cron definition "invalid cron expression" is not valid! Cron not registered.');
            }
        };
        const errorPromise = new Promise<Error>((resolve) => task.error.subscribe(resolve));

        await activate(task);

        chai.expect((await errorPromise).message).to.contain("Cron definition");
        await task.onDestroy();
    });
});