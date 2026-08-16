import { Task, System, TaskBase, Utilities, DI } from "@criticalmanufacturing/connect-iot-controller-engine";
import moment from "moment";
import { CustomTimerType, CustomTimerWorkingMode, CustomTimeZone } from "../../types/types";
import { CustomCronHandler } from "./custom-cron-handler-node";

/** Default values for settings */
export const SETTINGS_DEFAULTS: CustomTimerSettings = {
    _autoActivate: true,
    _timerType: CustomTimerType.Timer,
    _timerWorkingMode: CustomTimerWorkingMode.UntilDeactivation,
    interval: 10000,
    _numberOfOccurrencesAllowed: 30,
    cronExpression: '',
    cronExpressionMap: {},
    _timeZone: CustomTimeZone.UTC,
    emitInNewContext: false
};

/**
 * @whatItDoes
 * This task enables the possibility to have in a workflow different types of timers (sleep, timeout, repeated timer and cron jobs).
 *
 * @howToUse
 * Whatever the timer type is, it will be triggered on one of the following conditions:
 *  - Auto Activate setting is set to true.
 *  - Activate input is fed with a true Boolean value (or compatible, like “1”, “true”, “t”, “yes”, “y”)
 *
 * If Activate input receives a false Boolean value, any active timer will be immediately deactivated.
 * If the Task is reactivated, all internal definitions will be reset (especially CurrentNumberOfOccurrences).
 * Entry point of a timer/stop watch
 *
 *
 * ### Inputs
 * * `integer` : **interval** - Timer interval
 * * `boolean` : **activate** - Based on the value, activate/deactivate the timer
 * * `string` : **cronExpression** - Cron Expression
 * * `Object` : **cronExpressionMap** - Cron expressions with the corresponding UID
 *
 * ### Outputs
 * * `date` : **timestamp** - When it was triggered
 * * `string` : **cronExpressionUID** - UID associated with the cron expression
 * * `Success` : **success** - Will be set on the following situations:
 *                              On Timer type Sleep: timer Elapsed;
 *                              On Timer type Timeout: timer deactivated before timeout time occurred;
 *                              On Timer type Timer: timer interval elapsed.
 *                              On Timer type CronJob: reaches the specified date and time
 *                              On Timer type ComplexCronJob: reaches the specified date and time
 * * `Error` : **error** - Will be set on the following situation:
 *                              On Timer type Sleep: never;
 *                              On Timer type Timeout: timeout occurred;
 *                              On Timer type Timer: never;
 *                              On Timer type CronJob: never;
 *                              On Timer type ComplexCronJob: never;
 * ### Settings
 * See {@see CustomTimerSettings}
 */
@Task.Task()
export class CustomTimerTask extends TaskBase implements CustomTimerSettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */


    /** **Outputs** */
    public timestamp: Task.Output<Date> = new Task.Output<Date>();
    /** Cron expression UID */
    public cronExpressionUID: Task.Output<string> = new Task.Output<string>();

    /**
     * Success output. Triggered on the following scenarios:
     *   type=Sleep: timer Elapsed;
     *   type=Timeout: timer deactivated before timeout time occurred;
     *   type=Timer: timer interval elapsed.
     *   type=CronJob: reaches the specified date and time
     *   type=ComplexCronJob: reaches the specified date and time
     */
    /**
     * Error output. Triggered in the following scenarios:
     *   type=Sleep: never;
     *   type=Timeout: timeout occurred;
     *   type=Timer: never;
     *   type=CronJob: never;
     *   type=ComplexCronJob: never;
     */

    /** Properties Settings */
    /** Auto activate the event listeners */
    _autoActivate: boolean = true;
    /** Timer Type to be used by the task */
    _timerType: CustomTimerType = CustomTimerType.Timer;
    /** Timer Working mode of the Timer task if Timer Type is set to Timer */
    _timerWorkingMode: CustomTimerWorkingMode = CustomTimerWorkingMode.UntilDeactivation;
    /** Timer interval value */
    interval: number = 10000;
    /** Number of occurrences that happened since last activation, used on Timer Type: Timer, TimerWorkingMode: NumberOfOccurrences */
    _numberOfOccurrences: number = 0;
    /** Number of occurrences that can happen since last activation until the next automatic deactivation
     *  used on Timer Type: Timer, TimerWorkingMode: NumberOfOccurrences */
    _numberOfOccurrencesAllowed: number = 30;
    /** Cron Job Definition */
    cronExpression: string;
    /** Cron Job Expression Map*/
    cronExpressionMap: Object;
    /** Time Zone for the mode CronJob */
    _timeZone: CustomTimeZone = CustomTimeZone.UTC;
    /** Create a new execution context when emitting an output */
    emitInNewContext: boolean = false;

    /** Flag indicating if the timer is already active or not */
    private _activated: boolean = false;
    private _waitTimer: ReturnType<typeof setTimeout> | undefined;

    /** The scheduled cron jobs */
    private _cronJobs: Array<any>;

    @DI.Inject('CoreTasksTimerTaskCron')
    private _cronHandler: CustomCronHandler;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {

        if (changes['activate']) {
            try {
                // Allow notification for the same value
                this.activate = undefined;
                const activateValue: boolean = Utilities.convertValueToType(changes['activate'].currentValue, Task.TaskValueType.Boolean, false);
                if (activateValue === true) {
                    await this.activateTimer();

                } else {
                    if (this._activated) {
                        this._logger.info(`${this._timerType} deactivated at '${moment().toDate()}'`);
                    }
                    await this.deActivateTimer();
                }
            } catch (error) {
                this.logAndEmitError(`Error on Activating timer: ${error}`);
            }
        }
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);

        this.interval = Utilities.convertValueToType(this.interval, Task.TaskValueType.Integer, 10000);
        this._autoActivate = Utilities.convertValueToType(this._autoActivate, Task.TaskValueType.Boolean, true);
        this._numberOfOccurrencesAllowed = Utilities.convertValueToType(this._numberOfOccurrencesAllowed, Task.TaskValueType.Integer, 30);
        this._cronJobs = [];

        if (this._autoActivate) {
            await this.activateTimer();
        }
    }

    /** Cleanup internal data, unregister any event handler, etc */
    public override async onDestroy(): Promise<void> {
        await this.deActivateTimer();
    }

    /**
     * Validates the cron expression. This method is used by the timer settings.
     *
     * @param cronExpression Cron Expression to validate
     */
    public validateCronExpression(cronExpression: string): boolean {
        return (this._cronHandler.validate(cronExpression));
    }

    /** Manage whether to run the code in a new execution or not */
    private manageExecutionContext(codeToExecute: () => void) {
        if (this.emitInNewContext === true) {
            const newExecutionContext = this._executionContext.fork({ properties: {} });
            newExecutionContext.run(codeToExecute);
        } else {
            codeToExecute();
        }
    }

    /** Destroy the timer */
    private async deActivateTimer(): Promise<void> {
        if (this._activated) {
            this._activated = false

            // case the timer type is cron job, stops the job scheduling
            if (this._timerType === CustomTimerType.CronJob || this._timerType === CustomTimerType.ComplexCronJob) {
                if (this._cronJobs != null && this._cronJobs.length !== 0) {
                    this._cronJobs.forEach((cronJob) => {
                        cronJob.destroy();
                    });
                    this._cronJobs = [];
                }
            } else {
                if (this._waitTimer) {
                    if (this._timerType === CustomTimerType.Timer) {
                        clearInterval(this._waitTimer);

                    } else {
                        clearTimeout(this._waitTimer);
                    }
                    this._waitTimer = undefined;

                    if (this._timerWorkingMode === CustomTimerWorkingMode.NumberOfOccurrences) {
                        this._numberOfOccurrences = 0;
                    }
                }
            }

        }
    }

    /** Create the timer */
    private async activateTimer(): Promise<void> {
        if (!this._activated) {
            this._activated = true;

            switch (this._timerType) {
                case CustomTimerType.Sleep:
                    this._logger.info(`Sleep activated with the interval='${this.interval} ms' at '${moment().toDate()}'`);
                    this._waitTimer = setTimeout(() => {
                        // Deactivate timer
                        this.manageExecutionContext(() => {
                            const momentDate = moment().toDate();
                            this._logger.info(`Sleep timer interval elapsed at '${momentDate}'`);
                            this.deActivateTimer();
                            this.timestamp.emit(momentDate);
                            this.success.emit(true);
                        });
                    }, this.interval);
                    break;

                case CustomTimerType.Timeout:
                    this._logger.info(`Timeout activated with the interval='${this.interval} ms' at '${moment().toDate()}'`);
                    this._waitTimer = setTimeout(() => {
                        // Deactivate timer
                        this.manageExecutionContext(() => {
                            const momentDate = moment().toDate();
                            this._logger.warning(`Timer interval elapsed: Timeout occurred at '${momentDate}'`);
                            this.deActivateTimer();
                            this.timestamp.emit(momentDate);
                            this.error.emit(new Error('Timeout occurred'));
                        });
                    }, this.interval);
                    break;

                case CustomTimerType.Timer:
                    if (this._timerWorkingMode === CustomTimerWorkingMode.NumberOfOccurrences) {
                        this._numberOfOccurrences = 0;
                    }
                    /** Timer will run and send a signal every [interval] ms
                    *  If TimerWorking mode is defined to UntilDeactivation it will run until deactivation
                    *  If TimerWorking mode is defined to NumberOfOccurrences timer will be deactivated
                    *       after [_numberOfOccurrences] is equal to [_numberOfOccurrencesAllowed],
                    *       any reactivation will reset the [_numberOfOccurrences] value
                    */
                    this._logger.info(`Timer activated with the interval='${this.interval} ms' at '${moment().toDate()}'`);
                    this._waitTimer = setInterval(() => {
                        this.manageExecutionContext(() => {
                            this.timestamp.emit(moment().toDate());
                            this.success.emit(true);
                            // Check if more runs are to be performed
                            if (this._timerWorkingMode === CustomTimerWorkingMode.NumberOfOccurrences) {
                                if (++this._numberOfOccurrences >= this._numberOfOccurrencesAllowed) {
                                    this.deActivateTimer();
                                }
                            }
                        });
                    }, this.interval);
                    break;

                case CustomTimerType.CronJob:
                    // starts a new cron job scheduling
                    // emits the timestamp and the success (true) every time the scheduling is reached
                    this._logger.info(`Cron Job activated with the expression '[${this.cronExpression}]'`);
                    this._cronJobs.push(this._cronHandler.setCronJob(this.cronExpression, () => {
                        this.manageExecutionContext(() => {
                            this._logger.info(`Cron Job scheduling reached the specified date and time`);
                            this.timestamp.emit(moment().toDate());
                            this.success.emit(true);
                        });
                    }, this._timeZone));
                    this._cronJobs.forEach((cronJob) => {
                        cronJob.start();
                    });
                    break;
                case CustomTimerType.ComplexCronJob:
                    // starts a new cron job scheduling for each uid
                    // emits the timestamp, the cron expression uid and the success (true) every time the scheduling is reached
                    Object.entries(this.cronExpressionMap).forEach(([key, value], index) => {
                        this._logger.info(`Cron Job with UID '[${key}]' activated with the expression '[${value}]'`);
                        this._cronJobs.push(this._cronHandler.setCronJob(value, () => {
                            this.manageExecutionContext(() => {
                                this._logger.info(`Cron Job ${key} scheduling reached the specified date and time`);
                                this.timestamp.emit(moment().toDate());
                                this.success.emit(true);
                                this.cronExpressionUID.emit(key);
                            });
                        }, this._timeZone));
                        this._cronJobs.forEach((cronJob) => {
                            cronJob.start();
                        });
                    });
                    break;
            }
        }
    }
}

// Add settings here
/** CustomTimer Settings object */
export interface CustomTimerSettings extends System.TaskDefaultSettings {
    /** Auto activate the event listeners */
    _autoActivate: boolean;
    /** Type of timer. Will define the behavior */
    _timerType: CustomTimerType;
    /** Working mode for timers of type Timer (every x ms) */
    _timerWorkingMode: CustomTimerWorkingMode;
    /** Number of ms to define the timer */
    interval: number;
    /** Number of occurrences allowed after activation, when TimerType:Timer and TimerWorkingMode:NumberOfOccurrences */
    _numberOfOccurrencesAllowed: number;
    /** Cron Job Expression - it specifies when the trigger will happen */
    cronExpression: string;
    /** Cron Job Expression Map- it specifies when the trigger for each uid will happen */
    cronExpressionMap: Object;
    /** Cron Job time zone */
    _timeZone: CustomTimeZone;
    /** Create a new execution context when emitting an output */
    emitInNewContext: boolean;
}

@Task.TaskModule({
    task: CustomTimerTask, // Our Task
    providers: [
        {
            class: CustomCronHandler, // Component that we are injecting
            isSingleton: true, // Should this component be a Singleton for the whole Controller
            symbol: "CoreTasksTimerTaskCron", // Name that will be used to Inject the container in the dependency injection
            scope: Task.ProviderScope.Local, // Injection scope (Local; WorkflowPlan; Controller)
        }
    ]
})
export class CustomTimerModule { }
