import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import { LoadAutomationConfigurationTask } from "./loadAutomationConfiguration.task";
import { AutomationConfigurationDataHandler } from "../../persistence/implementation/automationConfigurationDataHandler";

@Task.TaskModule({
    task: LoadAutomationConfigurationTask,
    providers: [
        {
            class: AutomationConfigurationDataHandler,
            isSingleton: true,
            symbol: "GlobalAutomationConfigurationDataHandler",
            scope: Task.ProviderScope.Controller
        },
    ]
})
export class LoadAutomationConfigurationModule { }