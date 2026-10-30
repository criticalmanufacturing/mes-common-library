/* eslint-disable @typescript-eslint/quotes */
import type { IoTATLScriptContextTest } from '../../types';

export function resetOnAcceptAllEventsChange(): IoTATLScriptContextTest {
    return {
        _execute: function () {
            // PackagePacker: Start of Script

            this.settings['_events'] = [];
            delete this.settings["_event"];
            this.components.get('equipmentEvent').defaultValue = undefined;

            this.settings['_messageFullName'] = undefined;
            this.components.get('messageFullName').defaultValue = '';

            this.settings['_outputs'] = [];

            this.reloadTree();

            // PackagePacker: End of Script
        },
    };
}
