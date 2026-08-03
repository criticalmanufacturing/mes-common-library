import "reflect-metadata";
import * as inversify from "inversify";
import * as chai from "chai";
import * as chaiSpies from "chai-spies";
import { container as MainContainer } from "../../src/inversify.config";
import { TYPES as DRIVER_TYPES } from "@criticalmanufacturing/connect-iot-driver";
import { DeviceDriver, CommunicationState, TYPES as COMMUNICATION_TYPES } from "@criticalmanufacturing/connect-iot-driver";
import { TestUtilities } from "@criticalmanufacturing/connect-iot-driver/dist/test";
import { TYPES as COMMON_TYPES, Logger } from "@criticalmanufacturing/connect-iot-common";
import { GenericContainer, StartedTestContainer, Wait } from "testcontainers";
import { AMQPDeviceDriver } from "../../src/driverImplementation";
import { AMQPCommunicationSettings } from "../../src/communicationSettings";
import { LoggerMock } from "@criticalmanufacturing/connect-iot-common/dist/test";
import * as os from "os";

chai.use(chaiSpies);

describe("Connection Tests", () => {
    let container: inversify.Container;
    let startTestContainer: StartedTestContainer;
    let amqpPort: number;
    let hostname: string;

    before(async () => {
        hostname = os.hostname();
        startTestContainer = await new GenericContainer("rabbitmq:4-management")
            .withExposedPorts(5672, 15672) // AMQP, Management UI
            .withEnvironment({
                "RABBITMQ_DEFAULT_USER": "guest",
                "RABBITMQ_DEFAULT_PASS": "guest"
            })
            .withWaitStrategy(Wait.forLogMessage(/Server startup complete/))
            .withStartupTimeout(60000)
            .start();

        amqpPort = startTestContainer.getMappedPort(5672);
    });

    beforeEach((done) => {
        MainContainer.snapshot();
        container = new inversify.Container();
        container.parent = MainContainer;

        // in this case, the logger is deeper and must be removed, otherwise the subscription is logging to the console
        if (container.parent && container.parent.parent) {
            container.parent.parent.rebind(COMMON_TYPES.Logger).to(LoggerMock).inSingletonScope();
        }

        container.bind<Logger>(COMMON_TYPES.Logger).to(LoggerMock).inSingletonScope();

        container.parent.bind<string>(DRIVER_TYPES.DriverId).toConstantValue("test-driver-id");
        container.bind<DeviceDriver>(COMMUNICATION_TYPES.Device.Driver).to(AMQPDeviceDriver as any).inSingletonScope();

        container.bind("Configurations").toConstantValue({
            commands: [],
            communication: {},
            events: [],
            properties: []
        });

        done();
    });

    it("Connect to RabbitMQ", async () => {

        container.rebind("Configurations").toConstantValue({
            commands: [],
            communication: {},
            events: [],
            properties: []
        });

        const driver: AMQPDeviceDriver = container.get<AMQPDeviceDriver>(COMMUNICATION_TYPES.Device.Driver);
        const configs: any = container.get("Configurations");
        const comSettings = (configs.communication as AMQPCommunicationSettings);
        comSettings.address = hostname;
        comSettings.port = amqpPort;

        await driver.initialize();
        await driver.setConfigurations(configs);
        await driver.connect();

        await TestUtilities.waitForNoError(2000, "CommunicationState is not 'Setup'", () => {
            chai.expect(driver.communicationState).to.equal(CommunicationState.Setup);
        });
        await driver.setupResult(true);

        await TestUtilities.waitForNoError(2000, "CommunicationState is not 'Communicating'", () => {
            chai.expect(driver.communicationState).to.equal(CommunicationState.Communicating);
        });
    });

    it("Connect to RabbitMQ Basic Auth - Happy Path", async () => {
        await createUserInRabbitMQ(startTestContainer, hostname);

        container.rebind("Configurations").toConstantValue({
            commands: [],
            communication: {
                "username": "testuser",
                "password": "testpass123"
            },
            events: [],
            properties: []
        });

        const driver: AMQPDeviceDriver = container.get<AMQPDeviceDriver>(COMMUNICATION_TYPES.Device.Driver);
        const configs: any = container.get("Configurations");
        const comSettings = (configs.communication as AMQPCommunicationSettings);
        comSettings.address = hostname;
        comSettings.port = amqpPort;

        await driver.initialize();
        await driver.setConfigurations(configs);
        await driver.connect();

        await TestUtilities.waitForNoError(2000, "CommunicationState is not 'Setup'", () => {
            chai.expect(driver.communicationState).to.equal(CommunicationState.Setup);
        });
        await driver.setupResult(true);

        await TestUtilities.waitForNoError(2000, "CommunicationState is not 'Communicating'", () => {
            chai.expect(driver.communicationState).to.equal(CommunicationState.Communicating);
        });
    });

    it("Connect to RabbitMQ Basic Auth - Wrong Auth", async () => {

        await createUserInRabbitMQ(startTestContainer, hostname);

        container.rebind("Configurations").toConstantValue({
            commands: [],
            communication: {
                "username": "admin123",
                "password": "admin1",
                "connectingTimeout": 1000
            },
            events: [],
            properties: []
        });

        const driver: AMQPDeviceDriver = container.get<AMQPDeviceDriver>(COMMUNICATION_TYPES.Device.Driver);
        const spy = chai.spy.on(driver, "notifyCommunicationStateChanged");
        const configs: any = container.get("Configurations");
        const comSettings = (configs.communication as AMQPCommunicationSettings);
        comSettings.address = hostname;
        comSettings.port = amqpPort;

        await driver.initialize();
        await driver.setConfigurations(configs);
        await driver.connect();

        await TestUtilities.waitForNoError(2000, "State should change to Disconnected", () => {
            chai.expect(spy).to.be.called.with(CommunicationState.Disconnecting, CommunicationState.Disconnected);
        });
    });

    afterEach(async () => {
        chai.spy.restore();
        const driver: AMQPDeviceDriver = container.get<AMQPDeviceDriver>(COMMUNICATION_TYPES.Device.Driver);
        if (driver != null) {
            await driver.disconnect();
        }
        MainContainer.restore();
    });

    after(async () => {
        await startTestContainer.stop();
    });
});

async function createUserInRabbitMQ(startTestContainer: StartedTestContainer, hostname: string) {
    const managementPort = startTestContainer.getMappedPort(15672);
    const managementUrl = `http://${hostname}:${managementPort}`;
    const adminAuth = "Basic " + Buffer.from("guest:guest").toString("base64");

    // Create the user
    await fetch(`${managementUrl}/api/users/testuser`, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json",
            "Authorization": adminAuth
        },
        body: JSON.stringify({
            password: "testpass123",
            tags: ""
        })
    });

    // Set permissions for the user on the default vhost
    await fetch(`${managementUrl}/api/permissions/%2F/testuser`, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json",
            "Authorization": adminAuth
        },
        body: JSON.stringify({
            configure: ".*",
            write: ".*",
            read: ".*"
        })
    });
}