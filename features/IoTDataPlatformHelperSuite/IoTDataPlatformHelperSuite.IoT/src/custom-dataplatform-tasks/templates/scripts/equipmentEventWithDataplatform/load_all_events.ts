/**
 * Loads the entire list of events both in the initial templates of the protocol, Driver Definitions and Workflow
 * customization using the Custom Templates task.
 */
const dd = this.service.workflow.getTaskDriverEvents(this.service.definition);
const custom = this.service.container.driverDefinitionTemplate.getDriverDefinitionEvents(this.service.definition.driver);

const initialTemplates = custom.filter(c => c.isCustom == null);
const customTemplates = custom.filter(c => c.isCustom != null);

// Rules:
// 1. Initial templates that were not overriden by the Driver Definitions nor customization
// 2. Driver Definitions that were not overriden by customization
// 3. Full customization
const all = [
    ...initialTemplates.filter(i => !dd.some(d => d.Name === i.Name) && !customTemplates.some(c => c.Name === i.Name)),
    ...dd.filter(d => !customTemplates.some(c => c.Name === d.Name)),
    ...customTemplates
];

// Sort everything by name
all.sort((a, b) => (a.Name > b.Name) ? 1 : -1);

// Return the full sorted list
all;
