import "reflect-metadata";
import { Container } from "inversify";

import { DeviceDriver, TYPES as COMMUNICATION_TYPES, container as driverContainer } from "@criticalmanufacturing/connect-iot-driver";
import { TYPES } from "./types";
import { MTConnectDeviceDriver } from "./driverImplementation";
import { MTConnectHandler } from "./mTConnect/mTConnectHandler";
import { ExtensionHandler } from "./extensions";

const container = new Container();
container.parent = driverContainer;
container.parent?.bind<Container>(TYPES.Injector).toConstantValue(container);

container.bind("dummy").toConstantValue("dummy"); // Needed to bypass constructor issue with node 12.16 and eventemitter
container.bind<MTConnectHandler>(TYPES.MTConnectHandler).to(MTConnectHandler).inSingletonScope();
container.bind<ExtensionHandler>(TYPES.ExtensionHandler).to(ExtensionHandler).inSingletonScope();

// Must place in parent otherwise the driver(common) will not find this
container.parent?.bind<DeviceDriver>(COMMUNICATION_TYPES.Device.Driver).to(MTConnectDeviceDriver).inSingletonScope();

export { container };
