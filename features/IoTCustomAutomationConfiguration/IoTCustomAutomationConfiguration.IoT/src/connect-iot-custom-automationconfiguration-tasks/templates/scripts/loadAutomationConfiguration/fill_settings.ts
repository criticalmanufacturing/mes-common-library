if (!this.settings._outputs || this.settings._outputs.length === 0) {
    this.settings._outputs = [];
    const parameters = this._automationProtocolParameters = this.service.workflow.getTaskDriverProtocolParameters(this.service.definition);
    for (const parameter of parameters) {
        const outputSettings = {
            name: parameter.Name,
            label: parameter.Label,
            defaultValue: parameter.DefaultValue,
            parameter: parameter,
            dataType: this.ExtendedDataFormUtil.convertEnums(parameter.DataType),
            automationDataType: parameter.DataType,
            referenceType: parameter.ReferenceType || Cmf.Foundation.BusinessObjects.AutomationReferenceType.None,
            description: parameter.Description
        };
        if (parameter.DataType === Cmf.Foundation.BusinessObjects.AutomationDataType.ExtensionParameters) {
            outputSettings.defaultValue = [];
            outputSettings.dataType = "Object";
        }
        this.settings._outputs.push(outputSettings);
    }
}
