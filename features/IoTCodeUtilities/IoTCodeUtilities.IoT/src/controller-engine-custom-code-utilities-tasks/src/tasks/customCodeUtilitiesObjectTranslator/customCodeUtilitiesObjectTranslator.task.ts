import { Task, System, TaskBase, TYPES, DI } from "@criticalmanufacturing/connect-iot-controller-engine";
import { ID } from "./customCodeUtilitiesObjectTranslator.task.util";
import { CustomCodeUtilitiesObjectTranslator } from "./customCodeUtilitiesObjectTranslator.task.util";

/**
 * @whatItDoes
 *
 * This task injects custom code utilities API into the task library, making it available for other tasks to use.
 *
 * @howToUse
 *
 * Drag and drop this task into your process, and it will automatically register the custom code utilities API in the task library. Other tasks can then access the API by referencing the ID defined in this task.
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 *
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 */
@Task.Task()
export class CustomCodeUtilitiesObjectTranslatorTask extends TaskBase {

    @DI.Inject(TYPES.Task.Library)
    public taskCodeExecutionLibs: Task.Library;

    public override async onBeforeInit(): Promise<void> {
        if (this.taskCodeExecutionLibs != null) {
            if (this.taskCodeExecutionLibs.implementations[ID] == null) {
                this.taskCodeExecutionLibs.addImplementation(ID, new CustomCodeUtilitiesObjectTranslator());
            }
        }
    }
}


