import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import { CSharpRoslynCodeTask } from "./c-sharp-roslyn-code.task";
import { ROSLYN_EXECUTION_MANAGER_SYMBOL } from "./roslyn/roslynExecutionManager";
import { RoslynExecutionManagerBrowser } from "./roslyn/roslynExecutionManagerBrowser";

@Task.TaskModule({
    task: CSharpRoslynCodeTask,
    providers: [{
        class: RoslynExecutionManagerBrowser,
        isSingleton: true,
        symbol: ROSLYN_EXECUTION_MANAGER_SYMBOL,
        scope: Task.ProviderScope.Local
    }]
})
export class CSharpRoslynCodeModule { }