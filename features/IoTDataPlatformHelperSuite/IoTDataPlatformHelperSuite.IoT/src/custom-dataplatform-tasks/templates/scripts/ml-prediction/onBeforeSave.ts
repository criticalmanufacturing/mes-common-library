import type { IoTATLScriptContextTest } from '../../types';

export function onBeforeSave(): IoTATLScriptContextTest {
    return {
        _execute: function () {
            // PackagePacker: Start of Script
            if (this.settings.mlModel != null) {
                this.settings.mlModelName = this.settings.mlModel.Name;
                this.settings.mlModelRevision = this.settings.mlModel.Revision;
                delete this.settings.mlModel;
            }
            // PackagePacker: End of Script
        },
    };
}