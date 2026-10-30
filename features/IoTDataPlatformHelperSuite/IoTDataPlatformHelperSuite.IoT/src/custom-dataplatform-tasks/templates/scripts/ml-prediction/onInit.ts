import type { IoTATLScriptContextTest } from '../../types';

export function onInit(): IoTATLScriptContextTest {
    return {
        _execute: function () {
            // PackagePacker: Start of Script
            const SETTINGS_DEFAULTS = {
                inputs: [],
                outputs: [],
                mlModelName: '',
                mlModelRevision: '',
            };

            const currentSettings = Object.assign({}, this.settings);
            Object.assign(this.settings, SETTINGS_DEFAULTS, currentSettings);

            if (
                this.settings.mlModelName != null &&
                this.settings.mlModelName.length > 0 &&
                this.settings.mlModelRevision != null &&
                this.settings.mlModelRevision.length > 0
            ) {
                this.settings.mlModel = {
                    Name: this.settings.mlModelName,
                    Revision: this.settings.mlModelRevision,
                };
            }

            this.reloadTree();
            // PackagePacker: End of Script
        },
    };
}