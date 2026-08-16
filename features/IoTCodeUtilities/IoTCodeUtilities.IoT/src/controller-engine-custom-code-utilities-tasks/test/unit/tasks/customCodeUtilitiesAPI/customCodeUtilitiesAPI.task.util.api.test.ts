import { System } from "@criticalmanufacturing/connect-iot-controller-engine";
import { LoggerMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/logger.mock";
import { expect } from "chai";

import {
    CustomUtilitiesUtilApi,
    ID,
    SystemApiUtilsDefaults,
    SystemApiUtilsSettings
} from "../../../../src/tasks/customCodeUtilitiesAPI/customCodeUtilitiesAPI.task.util.api";

import EntityTypeManagement = System.LBOS.Cmf.Foundation.BusinessOrchestration.EntityTypeManagement;
import GenericServiceManagement = System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement;
import ConnectIoTManagement = System.LBOS.Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement;
import QueryManagement = System.LBOS.Cmf.Foundation.BusinessOrchestration.QueryManagement;
import AutomationSystemState = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationSystemState;
import AutomationCommunicationState = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommunicationState;
import SetAutomationStateType = System.LBOS.Cmf.Foundation.BusinessObjects.SetAutomationStateType;

describe("CustomUtilitiesUtilApi tests", () => {

    // Fast settings so retry tests don't wait for the (1s) production defaults
    const FAST_SETTINGS: SystemApiUtilsSettings = { maxRetries: 5, sleepBetweenRetries: 1 };

    let api: CustomUtilitiesUtilApi;
    let calls: any[];
    let framework: any;

    beforeEach(() => {
        api = new CustomUtilitiesUtilApi();
        calls = [];
        framework = {
            logger: new LoggerMock(),
            system: {
                call: async (input: any) => {
                    calls.push(input);
                    return {};
                }
            }
        };
    });

    describe("constants", () => {
        it("should export the expected implementation id", () => {
            expect(ID).to.equal("customCodeUtilitiesAPI");
        });

        it("should export the expected default settings", () => {
            expect(SystemApiUtilsDefaults).to.deep.equal({ maxRetries: 30, sleepBetweenRetries: 1000 });
        });
    });

    describe("getObjectById", () => {
        it("should get an object by id using the defaults", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Instance: { Id: "123", Name: "Object123" } };
            };

            const result = await api.getObjectById(framework, "123", "MyType");

            expect(result).to.deep.equal({ Id: "123", Name: "Object123" });
            expect(calls).to.have.lengthOf(1);
            expect(calls[0]).to.be.an.instanceof(GenericServiceManagement.InputObjects.GetObjectByIdInput);
            expect(calls[0].Id).to.equal("123");
            expect(calls[0].Type).to.equal("MyType");
            expect(calls[0].LevelsToLoad).to.equal(0);
        });

        it("should propagate the requested levelsToLoad", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Instance: {} };
            };

            await api.getObjectById(framework, "123", "MyType", 3);

            expect(calls[0].LevelsToLoad).to.equal(3);
        });

        it("should resolve the type name first when typeIsTypeId is true", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                if (input instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput) {
                    return { EntityType: { Name: "ResolvedTypeName" } };
                }
                return { Instance: { Id: "123" } };
            };

            await api.getObjectById(framework, "123", "TypeId123", 2, true);

            expect(calls).to.have.lengthOf(2);
            expect(calls[0]).to.be.an.instanceof(EntityTypeManagement.InputObjects.GetEntityTypeByIdInput);
            expect(calls[0].Id).to.equal("TypeId123");
            expect(calls[1]).to.be.an.instanceof(GenericServiceManagement.InputObjects.GetObjectByIdInput);
            expect(calls[1].Type).to.equal("ResolvedTypeName");
        });

        it("should cache the resolved type name across calls", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                if (input instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput) {
                    return { EntityType: { Name: "ResolvedTypeName" } };
                }
                return { Instance: { Id: "123" } };
            };

            await api.getObjectById(framework, "123", "TypeId123", 0, true);
            await api.getObjectById(framework, "456", "TypeId123", 0, true);

            // Only one entity-type resolution call, even though it was requested twice
            const resolutionCalls = calls.filter((call) => call instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput);
            expect(resolutionCalls).to.have.lengthOf(1);
            expect(calls).to.have.lengthOf(3);
        });
    });

    describe("getObjectByName", () => {
        it("should get an object by name using the defaults", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Instance: { Id: "123", Name: "ObjectName" } };
            };

            const result = await api.getObjectByName(framework, "ObjectName", "MyType");

            expect(result).to.deep.equal({ Id: "123", Name: "ObjectName" });
            expect(calls[0]).to.be.an.instanceof(GenericServiceManagement.InputObjects.GetObjectByNameInput);
            expect(calls[0].Name).to.equal("ObjectName");
            expect(calls[0].Type).to.equal("MyType");
            expect(calls[0].LevelsToLoad).to.equal(0);
        });

        it("should resolve the type name first when typeIsTypeId is true", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                if (input instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput) {
                    return { EntityType: { Name: "ResolvedTypeName" } };
                }
                return { Instance: { Name: "ObjectName" } };
            };

            await api.getObjectByName(framework, "ObjectName", "TypeId123", 1, true);

            expect(calls).to.have.lengthOf(2);
            expect(calls[1].Type).to.equal("ResolvedTypeName");
        });

        it("should propagate the requested levelsToLoad", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Instance: { Name: "ObjectName" } };
            };

            await api.getObjectByName(framework, "ObjectName", "MyType", 4);

            expect(calls[0].LevelsToLoad).to.equal(4);
        });

        it("should reuse a resolved type id from an object-by-id lookup", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                if (input instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput) {
                    return { EntityType: { Name: "ResolvedTypeName" } };
                }
                return { Instance: {} };
            };

            await api.getObjectById(framework, "123", "TypeId123", 0, true);
            await api.getObjectByName(framework, "ObjectName", "TypeId123", 0, true);

            const resolutionCalls = calls.filter((call) => call instanceof EntityTypeManagement.InputObjects.GetEntityTypeByIdInput);
            expect(resolutionCalls).to.have.lengthOf(1);
            expect(calls).to.have.lengthOf(3);
            expect(calls[2].Type).to.equal("ResolvedTypeName");
        });
    });

    describe("loadAttributes", () => {
        const entity = { Id: "1", Name: "Entity1" };

        it("should load all attributes when none are specified", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Entity: { ...entity, SomeAttribute: "value" } };
            };

            const result = await api.loadAttributes(framework, entity);

            expect(calls[0]).to.be.an.instanceof(GenericServiceManagement.InputObjects.LoadObjectAttributesInput);
            expect(calls[0].Entity).to.equal(entity);
            expect(calls[0].Attributes).to.be.undefined;
            expect(result.SomeAttribute).to.equal("value");
        });

        it("should load only the specified attributes", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Entity: entity };
            };

            await api.loadAttributes(framework, entity, ["Attribute1", "Attribute2"]);

            expect(calls[0].Attributes).to.deep.equal(["Attribute1", "Attribute2"]);
        });

        it("should not set Attributes when specificAttributes is an empty array", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { Entity: entity };
            };

            await api.loadAttributes(framework, entity, []);

            expect(calls[0].Attributes).to.be.undefined;
        });
    });

    describe("executeQuery", () => {
        const queryObject = { Name: "MyQuery", EntityTypeName: "Foo" };

        it("should execute a query and return the NgpDataSet", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { NgpDataSet: { T_Result: [{ id: 1 }, { id: 2 }] } };
            };

            const result = await api.executeQuery(framework, queryObject);

            expect(calls[0]).to.be.an.instanceof(QueryManagement.InputObjects.ExecuteQueryInput);
            expect(calls[0].QueryObject).to.equal(queryObject);
            expect(calls[0].QueryParameters).to.be.undefined;
            expect(result.T_Result).to.have.lengthOf(2);
        });

        it("should forward the parameter collection when provided", async () => {
            const parameters = { Foo: "Bar" };
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { NgpDataSet: {} };
            };

            await api.executeQuery(framework, queryObject, parameters);

            expect(calls[0].QueryParameters).to.equal(parameters);
        });

        it("should return undefined without retrying when the query yields no dataset", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                return { NgpDataSet: undefined };
            };

            const result = await api.executeQuery(framework, queryObject);

            expect(result).to.be.undefined;
            expect(calls).to.have.lengthOf(1);
        });
    });

    describe("setInstanceSystemState", () => {
        it("should update the system state of a controller instance and ignore any communication state", async () => {
            await api.setInstanceSystemState(
                framework,
                "AutomationControllerInstance/123",
                AutomationSystemState.Running,
                AutomationCommunicationState.Communicating
            );

            expect(calls).to.have.lengthOf(1);
            expect(calls[0]).to.be.an.instanceof(ConnectIoTManagement.InputObjects.SetAutomationStateInput);
            expect(calls[0].Id).to.equal("123");
            expect(calls[0].Type).to.equal(SetAutomationStateType.AutomationControllerInstance);
            expect(calls[0].SystemState).to.equal(AutomationSystemState.Running);
            expect(calls[0].CommunicationState).to.be.undefined;
            expect(calls[0].IgnoreLastServiceId).to.equal(true);
        });

        it("should update both system and communication state of a driver instance", async () => {
            await api.setInstanceSystemState(
                framework,
                "AutomationDriverInstance/456",
                AutomationSystemState.Fault,
                AutomationCommunicationState.Disconnected
            );

            expect(calls[0].Type).to.equal(SetAutomationStateType.AutomationDriverInstance);
            expect(calls[0].SystemState).to.equal(AutomationSystemState.Fault);
            expect(calls[0].CommunicationState).to.equal(AutomationCommunicationState.Disconnected);
        });

        it("should update only the communication state of a driver instance when no system state is supplied", async () => {
            await api.setInstanceSystemState(
                framework,
                "AutomationDriverInstance/456",
                undefined,
                AutomationCommunicationState.Communicating
            );

            expect(calls).to.have.lengthOf(1);
            expect(calls[0].Type).to.equal(SetAutomationStateType.AutomationDriverInstance);
            expect(calls[0].SystemState).to.be.undefined;
            expect(calls[0].CommunicationState).to.equal(AutomationCommunicationState.Communicating);
        });

        it("should throw for an unrecognized instance type and make no system call", async () => {
            let thrown = false;

            try {
                await api.setInstanceSystemState(framework, "SomeOtherInstance/123", AutomationSystemState.Running);
            } catch (error) {
                thrown = true;
                expect(error.message).to.equal("Invalid instance type 'SomeOtherInstance'. No state change will be performed.");
            }

            expect(thrown).to.equal(true);
            expect(calls).to.have.lengthOf(0);
        });

        it("should throw for a malformed instanceId and make no system call", async () => {
            let thrown = false;

            try {
                await api.setInstanceSystemState(framework, "NoSlashHere");
            } catch (error) {
                thrown = true;
                expect(error.message).to.equal("Invalid instanceId 'NoSlashHere' provided. No state change will be performed.");
            }

            expect(thrown).to.equal(true);
            expect(calls).to.have.lengthOf(0);
        });
    });

    describe("retry behaviour", () => {
        it("should retry on a known transient system error and eventually succeed", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                if (calls.length < 3) {
                    throw new Error("The data for object Foo has changed since last viewed. Please refresh the object.");
                }
                return { Instance: { Id: "42" } };
            };

            const result = await api.getObjectById(framework, "42", "MyType", 0, false, FAST_SETTINGS);

            expect(result).to.deep.equal({ Id: "42" });
            expect(calls).to.have.lengthOf(3);
        });

        it("should throw after exhausting all retries for a persistently failing transient error", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                throw new Error("Error: connect ECONNREFUSED ");
            };
            let thrown = false;

            try {
                await api.getObjectById(framework, "42", "MyType", 0, false, FAST_SETTINGS);
            } catch (error) {
                thrown = true;
                expect(error.message).to.equal("Error: connect ECONNREFUSED ");
            }

            expect(thrown).to.equal(true);
            expect(calls).to.have.lengthOf(FAST_SETTINGS.maxRetries);
        });

        it("should not retry on an unknown, non-retryable error", async () => {
            framework.system.call = async (input: any) => {
                calls.push(input);
                throw new Error("Some unexpected failure");
            };
            let thrown = false;

            try {
                await api.getObjectById(framework, "42", "MyType", 0, false, FAST_SETTINGS);
            } catch (error) {
                thrown = true;
                expect(error.message).to.equal("Some unexpected failure");
            }

            expect(thrown).to.equal(true);
            expect(calls).to.have.lengthOf(1);
        });
    });
});
