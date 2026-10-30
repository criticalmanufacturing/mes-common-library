import "reflect-metadata";
import * as chai from "chai";
import chaiSpies = require("chai-spies");
import { expect } from "chai";

import { QueueManagerType, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
import { LoggerMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/logger.mock";
import { SystemProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/system-proxy.mock";

import { BatchPostEventHandler, BatchPostConfig } from "../../../src/context/batchPostEventHandler";
import { BatchedTelemetryEvent, PostTelemetry } from "../../../src/utilities/interfaces";

chai.use(chaiSpies);

describe("BatchPostEventHandler tests", () => {

    let handler: BatchPostEventHandler;
    let dataStore: DataStoreMock;
    let logger: LoggerMock;
    let systemProxy: SystemProxyMock;
    let errors: string[];

    const makeConfig = (overrides: Partial<BatchPostConfig> = {}): BatchPostConfig => ({
        applicationName: "TestApp",
        intervalMs: 100000,
        batchSize: 3,
        numberOfRetries: 1,
        iotRetries: 1,
        iotSleepBetweenRetries: 0,
        ...overrides
    });

    const makeEvent = (index: number): BatchedTelemetryEvent => ({
        data: { Tags: [{ Key: "Index", Value: String(index) }] } as PostTelemetry,
        eventTime: Date.now().toString()
    });

    beforeEach(() => {
        handler = new BatchPostEventHandler();
        dataStore = new DataStoreMock();
        logger = new LoggerMock();
        systemProxy = new SystemProxyMock();
        errors = [];
        (handler as any)._dataStore = dataStore;
        (handler as any)._systemProxy = systemProxy;
        (handler as any)._logger = logger;
    });

    afterEach(() => {
        handler.dispose();
    });

    it("should not post anything before batchSize or the timer interval is reached", async () => {
        chai.spy.on(systemProxy, "call", () => Promise.resolve({ HasErrors: false }));
        const config = makeConfig({ batchSize: 5, intervalMs: 100000 });

        await handler.enqueue(config, makeEvent(1));
        await handler.enqueue(config, makeEvent(2));
        await Utilities.sleep(20);

        expect(systemProxy.call).to.not.have.been.called();
    });

    it("should flush automatically once batchSize pending events are reached", async () => {
        let receivedInput: any;
        chai.spy.on(systemProxy, "call", (input: any) => {
            receivedInput = input;
            return Promise.resolve({ HasErrors: false });
        });

        const config = makeConfig({ batchSize: 3, intervalMs: 100000 });
        await handler.enqueue(config, makeEvent(1));
        await handler.enqueue(config, makeEvent(2));
        await handler.enqueue(config, makeEvent(3));
        await Utilities.sleep(20);

        expect(systemProxy.call).to.have.been.called.once;
        expect(receivedInput.IoTEvents).to.have.length(3);
        expect(receivedInput.IoTEvents[0].AppProperties.ApplicationName).to.equal("TestApp");
    });

    it("should flush automatically when the timer interval elapses, even below batchSize", async () => {
        let receivedInput: any;
        chai.spy.on(systemProxy, "call", (input: any) => {
            receivedInput = input;
            return Promise.resolve({ HasErrors: false });
        });

        const config = makeConfig({ batchSize: 100, intervalMs: 20 });
        await handler.enqueue(config, makeEvent(1));
        await Utilities.sleep(60);

        expect(systemProxy.call).to.have.been.called();
        expect(receivedInput.IoTEvents).to.have.length(1);
    });

    it("should reuse the same timer/queue for repeated enqueue calls with the same applicationName", async () => {
        const flushSpy = chai.spy.on(systemProxy, "call", () => Promise.resolve({ HasErrors: false }));

        const config = makeConfig({ batchSize: 2, intervalMs: 100000 });
        await handler.enqueue(config, makeEvent(1));
        await handler.enqueue(config, makeEvent(2));
        await Utilities.sleep(20);

        await handler.enqueue(config, makeEvent(3));
        await handler.enqueue(config, makeEvent(4));
        await Utilities.sleep(20);

        expect(flushSpy).to.have.been.called.exactly(2);
    });

    it("should keep queuing separately per applicationName", async () => {
        const receivedInputs: any[] = [];
        chai.spy.on(systemProxy, "call", (input: any) => {
            receivedInputs.push(input);
            return Promise.resolve({ HasErrors: false });
        });

        const configA = makeConfig({ applicationName: "AppA", batchSize: 1, intervalMs: 100000 });
        const configB = makeConfig({ applicationName: "AppB", batchSize: 1, intervalMs: 100000 });

        await handler.enqueue(configA, makeEvent(1));
        await handler.enqueue(configB, makeEvent(2));
        await Utilities.sleep(20);

        expect(receivedInputs).to.have.length(2);
        expect(receivedInputs.map((i) => i.IoTEvents[0].AppProperties.ApplicationName).sort())
            .to.deep.equal(["AppA", "AppB"]);
    });

    it("should report an error via onError and not throw when posting fails", async () => {
        chai.spy.on(systemProxy, "call", () => Promise.reject(new Error("boom")));
        chai.spy.on(logger, "error", (message: string) => { errors.push(message); });

        const config = makeConfig({ batchSize: 1, iotRetries: 1 });
        await handler.enqueue(config, makeEvent(1));
        await Utilities.sleep(20);

        expect(errors).to.have.length(1);
        expect(errors[0]).to.include("boom");
    });

    it("should persist events in the DataStore queue when enqueued", async () => {
        const config = makeConfig({ batchSize: 100, intervalMs: 100000 });
        const event = makeEvent(1);
        await handler.enqueue(config, event);

        const queued = await dataStore.retrieveFromQueue(`BatchPostEvents_${config.applicationName}`, QueueManagerType.FIFO, null);
        expect(queued).to.deep.equal(event);
    });

    describe("setting precedence (task > controller config > hard-coded default)", () => {

        it("should use the controller config.json value when the task does not set batchSize", async () => {
            (handler as any)._configuration = { data: { controller: { dataplatform: { batchSize: 2 } } } };
            const flushSpy = chai.spy.on(systemProxy, "call", () => Promise.resolve({ HasErrors: false }));

            const config = makeConfig({ batchSize: undefined, intervalMs: 100000 });
            await handler.enqueue(config, makeEvent(1));
            await Utilities.sleep(10);
            expect(flushSpy).to.not.have.been.called();

            await handler.enqueue(config, makeEvent(2));
            await Utilities.sleep(10);
            expect(flushSpy).to.have.been.called.once;
        });

        it("should let an explicit task batchSize take precedence over the controller config.json value", async () => {
            (handler as any)._configuration = { data: { controller: { dataplatform: { batchSize: 100 } } } };
            const flushSpy = chai.spy.on(systemProxy, "call", () => Promise.resolve({ HasErrors: false }));

            const config = makeConfig({ batchSize: 1, intervalMs: 100000 });
            await handler.enqueue(config, makeEvent(1));
            await Utilities.sleep(10);

            expect(flushSpy).to.have.been.called.once;
        });

        it("should fall back to the hard-coded default when neither task nor controller config.json set numberOfRetries", async () => {
            let receivedInput: any;
            chai.spy.on(systemProxy, "call", (input: any) => {
                receivedInput = input;
                return Promise.resolve({ HasErrors: false });
            });

            const config = makeConfig({ numberOfRetries: undefined, intervalMs: 5, batchSize: 100 });
            await handler.enqueue(config, makeEvent(1));
            await Utilities.sleep(30);

            expect(receivedInput.IoTEvents[0].NumberOfRetries).to.equal(1);
        });
    });
});
