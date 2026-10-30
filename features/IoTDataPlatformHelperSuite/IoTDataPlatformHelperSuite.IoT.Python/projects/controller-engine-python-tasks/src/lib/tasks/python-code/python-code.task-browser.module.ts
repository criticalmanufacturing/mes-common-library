import { Task } from '@criticalmanufacturing/connect-iot-controller-engine';
import { PythonCodeTask } from './python-code.task';
import { PYODIDE_MANAGER_SYMBOL } from './pyodide';
import { PyodideManagerBrowser } from './pyodide/pyodideManagerBrowser';

@Task.TaskModule({
    task: PythonCodeTask,
    providers: [
        {
            class: PyodideManagerBrowser,
            isSingleton: true,
            symbol: PYODIDE_MANAGER_SYMBOL,
            scope: Task.ProviderScope.Local,
        }
    ]
})
export class PythonCodeModule { }