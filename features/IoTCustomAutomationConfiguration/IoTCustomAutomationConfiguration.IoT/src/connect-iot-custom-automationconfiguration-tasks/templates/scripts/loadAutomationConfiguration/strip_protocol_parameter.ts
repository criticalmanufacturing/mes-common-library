if (this.settings._outputs != null && this.settings._outputs.length > 0) {
    for (let output of this.settings._outputs) {
        output.parameter = this.utilities.stripAutomationEntity(output.parameter);
    }
}