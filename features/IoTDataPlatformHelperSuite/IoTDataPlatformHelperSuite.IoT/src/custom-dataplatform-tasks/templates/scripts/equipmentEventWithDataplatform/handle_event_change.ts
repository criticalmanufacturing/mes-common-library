/* eslint-disable @typescript-eslint/quotes */
import { DataWorkflowInstance } from 'cmf-core-connect-iot';
import type { IoTATLScriptContextTest } from '../../types';
import Cmf from 'cmf-lbos';

export function handleChangesEvent(): IoTATLScriptContextTest {
    return {
        _execute: function () {
            // PackagePacker: Start of Script
            const selectedEvent = this.settings["_event"] as Cmf.Foundation.BusinessObjects.AutomationEvent;

            const outputs = [];
            let messageFullName = '';

            if (selectedEvent != null) {
                // A driver definition event had the Id filled in with the System Id. All customization doesn't have this field
                const isDriverDefinition = selectedEvent.Id != null;
                // Message full name is only used by Control flow for the time being. Should be removed after support for FindEntities
                messageFullName = selectedEvent.Name;

                const leafDefinition = this.service.container.taskMetadata.options.atlMetadata.outputs.placeholder.settings.leafDefinition;

                if (isDriverDefinition) {
                    const props = (this.service.workflow as DataWorkflowInstance).getTaskDriverProperties(this.service.definition, selectedEvent.Id);
                    for (const prop of props) {
                        const output = {
                            name: prop.Name,
                            propertyId: prop.DevicePropertyId,
                            property: this.utilities.stripAutomationEntity(prop, ['DataType', '$type']),
                            valueType: Cmf.Foundation.BusinessObjects.AutomationDataType[prop.DataType.toString()],
                            deviceDataType: prop.AutomationProtocolDataType.Name,
                            defaultValue: null,
                        };

                        for (const leaf of leafDefinition) {
                            if (!Object.keys(output).includes(leaf.settingKey)) {
                                output[leaf?.settingKey ?? leaf.name] = leaf.defaultValue;
                            }
                        }

                        outputs.push(output);
                    }
                } else {
                    const props = this.service.container.driverDefinitionTemplate.getDriverDefinitionProperties(this.service.definition.driver, selectedEvent.Name);
                    for (const prop of props) {
                        const output = {
                            name: prop.Name,
                            propertyId: prop.DevicePropertyId,
                            valueType: prop.DataType,
                            deviceDataType: prop.AutomationProtocolDataType,
                            defaultValue: null,
                        };

                        for (const leaf of leafDefinition) {
                            if (!Object.keys(output).includes(leaf.settingKey)) {
                                output[leaf?.settingKey ?? leaf.name] = leaf.defaultValue;
                            }
                        }
                        outputs.push(output);
                    }
                }
            }

            this.settings['_outputs'] = outputs;
            this.settings['_messageFullName'] = messageFullName;
            this.components.get('messageFullName').defaultValue = messageFullName ?? "";

            this.reloadTree();

            // PackagePacker: End of Script
        },
    };
}
