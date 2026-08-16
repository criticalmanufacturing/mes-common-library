import {
    injectable
} from "inversify";
import {
    validate,
    schedule
} from "node-cron";
import { Timezone } from "tz-offset";

@injectable()
export class CustomCronHandler {

    /**
    ###  Validate if the cron expression is valid
    ### ┌────────────── second (optional)
    ### │ ┌──────────── minute
    ### │ │ ┌────────── hour
    ### │ │ │ ┌──────── day of month
    ### │ │ │ │ ┌────── month
    ### │ │ │ │ │ ┌──── day of week
    ##  * * * * * *
    *
    * @param cronExpression Expression to validate
    */
    public validate(cronExpression: string): boolean {
        return (validate(cronExpression));

    }

    /**
     * Schedules a job to be executed whenever the cron expression triggers
     * @param cronExpression Expression that defines the job scheduling
     * @param executeCallback Job to execute when triggered
     * @param timeZone The timezone that is used for job scheduling;
     */
    public setCronJob(cronExpression: string, executeCallback: Function, timeZone: Timezone): any {

        // validates the cron expression before scheduling the job
        if (!this.validate(cronExpression)) {
            throw new Error(`Cron definition "${cronExpression}" is not valid! Cron not registered.`);
        } else {

            // schedules the job for a given moment and timezone
            return schedule(cronExpression, () => {
                executeCallback();
            }, {
                scheduled: false,
                timezone: <Timezone>timeZone
            });
        }
    }

    /**
    * Stops the scheduled task. The job won"t be executed unless re-started
    * @param cronJob Job scheduling
    */
    public stopCronJob(cronJob: any): void {
        cronJob.stop();
    }

    /**
     * Starts the scheduled task.
     * @param cronJob Job scheduling
     */
    public startCronJob(cronJob: any): void {
        cronJob.start();
    }

    /**
     * The task will be stopped and completely destroyed
     * @param cronJob Job scheduling
     */
    public destroyCronJob(cronJob: any): void {
        cronJob.destroy();
    }



}
