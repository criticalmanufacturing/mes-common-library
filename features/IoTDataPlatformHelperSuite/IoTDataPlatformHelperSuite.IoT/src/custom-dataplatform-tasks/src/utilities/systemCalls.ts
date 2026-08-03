import { Dependencies, System, Task, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import { BatchedTelemetryEvent, ISA95 } from "./interfaces";
import { Queries } from "./queries";
import Cmf from "cmf-lbos";
import DataPlatform = System.LBOS.Cmf.Foundation.BusinessOrchestration.DataPlatform;

// eslint-disable-next-line @typescript-eslint/no-var-requires
const moment = require("moment");
/**
 * Provides utility system calls to interact with ISA-95 model data
 * and to post telemetry events into the data platform.
 */
export class SystemCalls {

    /**
     * Extracts the ISA-95 model hierarchy (Enterprise, Site, Facility, Area, Resource)
     * from the provided system entity instance.
     */
    public static async extractISA95(
        instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity,
        system: System.SystemAPI,
        logAndEmitError: (message: string) => void
    ): Promise<ISA95> {
        let query;
        const isa95Result = {};

        switch (instance["$type"].toString()) {
            case "Cmf.Navigo.BusinessObjects.Material, Cmf.Navigo.BusinessObjects":
                query = Queries.getIsa95QueryFromMaterial(instance.Name);
                isa95Result["Material"] = instance.Name;
                break;
            case "Cmf.Navigo.BusinessObjects.Resource, Cmf.Navigo.BusinessObjects":
                query = Queries.getIsa95QueryFromResource(instance.Name);
                isa95Result["Resource"] = instance.Name;
                break;
            case "Cmf.Navigo.BusinessObjects.Area, Cmf.Navigo.BusinessObjects":
                query = Queries.getIsa95QueryFromArea(instance.Name);
                isa95Result["Area"] = instance.Name;
                break;
            case "Cmf.Navigo.BusinessObjects.Facility, Cmf.Navigo.BusinessObjects":
                query = Queries.getIsa95QueryFromFacility(instance.Name);
                isa95Result["Facility"] = instance.Name;
                break;
            case "Cmf.Foundation.BusinessObjects.Site, Cmf.Foundation.BusinessObjects":
                query = Queries.getIsa95QueryFromSite(instance.Name);
                isa95Result["Site"] = instance.Name;
                break;
            case "Cmf.Foundation.BusinessObjects.Enterprise, Cmf.Foundation.BusinessObjects":
                return { Enterprise: instance.Name };
            default:
                logAndEmitError(`This task can only be used with entities of the ISA-95`);
                return {};
        }

        const executeQueryObject = new Cmf.Foundation.BusinessOrchestration.QueryManagement.InputObjects.ExecuteQueryInput();
        executeQueryObject.QueryObject = query;

        const result = await system.call(executeQueryObject) as Cmf.Foundation.BusinessOrchestration.QueryManagement.OutputObjects.ExecuteQueryOutput;

        if (result != null && result.NgpDataSet && result.NgpDataSet["T_Result"]) {
            const resultRow = result.NgpDataSet["T_Result"][0];
            return {
                Material: resultRow.Material,
                Resource: resultRow.Resource,
                Area: resultRow.Area,
                Facility: resultRow.Facility,
                Site: resultRow.Site,
                Enterprise: resultRow.Enterprise
            };
        }

        return {};
    }

    /**
     * Posts telemetry data as an event to the data platform.
     */
    public static async postTelemetry(
        data: object,
        applicationName: string,
        ignoreLastServiceId: boolean,
        iotRetries: number,
        iotSleepBetweenRetries: number,
        numberOfRetries: number,
        systemProxy: System.SystemProxy,
        logger: Dependencies.Logger
    ): Promise<DataPlatform.OutputObjects.PostEventOutput> {
        logger.info("Posting a telemetry event");

        if (applicationName != null && applicationName.length > 0) {
            const output: DataPlatform.OutputObjects.PostEventOutput =
                await Utilities.ExecuteWithSystemErrorRetry(logger, iotRetries, iotSleepBetweenRetries, async () => {
                    return (await systemProxy.call<System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseOutput>(
                        SystemCalls.createCallPostTelemetry(data, applicationName, ignoreLastServiceId, numberOfRetries, logger)
                    ));
                });
            return output;
        } else {
            throw new Error("No application name provided");
        }
    }

    /**
     * Creates the PostEventInput object for telemetry posting.
     */
    private static createCallPostTelemetry(
        data: object,
        applicationName: string,
        ignoreLastServiceId: boolean,
        numberOfRetries: number,
        logger: Dependencies.Logger
    ): DataPlatform.InputObjects.PostEventInput {
        const input = new DataPlatform.InputObjects.PostEventInput();
        input.AppProperties = new DataPlatform.Domain.AppProperties();
        input.AppProperties.ApplicationName = applicationName;
        input.AppProperties.EventDefinition = "PostTelemetry";
        input.AppProperties.EventTime = moment.utc();
        input.IgnoreLastServiceId = Utilities.convertValueToType(ignoreLastServiceId, Task.TaskValueType.Boolean, false);
        input.NumberOfRetries = Utilities.convertValueToType(numberOfRetries, Task.TaskValueType.Integer, false);
        input.Data = data;
        logger.debug(` AppProperties: ${JSON.stringify(input.AppProperties)}\n Payload: ${JSON.stringify(input.Data)}`);
        return input;
    }

    /**
     * Posts a batch of previously queued telemetry events to the data platform in a single call.
     */
    public static async postTelemetryBatch(
        events: BatchedTelemetryEvent[],
        applicationName: string,
        numberOfRetries: number,
        iotRetries: number,
        iotSleepBetweenRetries: number,
        systemProxy: System.SystemProxy,
        logger: Dependencies.Logger
    ): Promise<DataPlatform.OutputObjects.PostMultipleIoTEventsOutput> {
        logger.info(`Posting a batch of ${events.length} telemetry event(s)`);

        if (applicationName != null && applicationName.length > 0) {
            const output: DataPlatform.OutputObjects.PostMultipleIoTEventsOutput =
                await Utilities.ExecuteWithSystemErrorRetry(logger, iotRetries, iotSleepBetweenRetries, async () => {
                    return (await systemProxy.call<System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseOutput>(
                        SystemCalls.createCallPostTelemetryBatch(events, applicationName, numberOfRetries, logger)
                    ));
                });
            return output;
        } else {
            throw new Error("No application name provided");
        }
    }

    /**
     * Creates the PostMultipleIoTEventsInput object for batched telemetry posting.
     */
    private static createCallPostTelemetryBatch(
        events: BatchedTelemetryEvent[],
        applicationName: string,
        numberOfRetries: number,
        logger: Dependencies.Logger
    ): DataPlatform.InputObjects.PostMultipleIoTEventsInput {
        const input = new DataPlatform.InputObjects.PostMultipleIoTEventsInput();
        input.NumberOfRetries = Utilities.convertValueToType(numberOfRetries, Task.TaskValueType.Integer, false);
        input.IoTEvents = events.map((event) => {
            const eventInput = new DataPlatform.InputObjects.PostEventInput();
            eventInput.AppProperties = new DataPlatform.Domain.AppProperties();
            eventInput.AppProperties.ApplicationName = applicationName;
            eventInput.AppProperties.EventDefinition = "PostTelemetry";
            eventInput.AppProperties.EventTime = moment.utc(Number(event.eventTime));
            eventInput.NumberOfRetries = input.NumberOfRetries;
            eventInput.Data = event.data;
            return eventInput;
        });
        logger.debug(` Batch AppProperties.ApplicationName: ${applicationName}\n Batch size: ${events.length}`);
        return input;
    }
}
