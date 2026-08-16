import { DataStoreLocation, System } from "@criticalmanufacturing/connect-iot-controller-engine";
import { expect } from "chai";

import {
    CustomCodeUtilitiesFramework,
    ID
} from "../../../../src/tasks/customCodeUtilitiesFramework/customCodeUtilitiesFramework.task.util";

describe("CustomCodeUtilitiesFramework tests", () => {
    let utility: CustomCodeUtilitiesFramework;
    let storedValues: any[];
    let systemCalls: any[];
    let framework: any;

    beforeEach(() => {
        utility = new CustomCodeUtilitiesFramework();
        storedValues = [];
        systemCalls = [];
        framework = {
            logger: { debug: () => undefined, info: () => undefined, warning: () => undefined },
            dataStore: {
                retrieve: async () => undefined,
                store: async (identifier: string, data: any, location: DataStoreLocation) => {
                    storedValues.push({ identifier, data, location });
                }
            },
            system: {
                call: async (input: any) => {
                    systemCalls.push(input);
                    return undefined;
                }
            }
        };
    });

    describe("basic helpers", () => {
        it("should export the expected implementation id", () => {
            expect(ID).to.equal("customCodeUtilitiesFramework");
        });

        it("should create stable hashes and identify nullish values", () => {
            expect(utility.createHashCode("configurationResourceA")).to.equal(utility.createHashCode("configurationResourceA"));
            expect(utility.createHashCode("configurationResourceA")).to.not.equal(utility.createHashCode("configurationResourceB"));
            expect(utility.createHashCode("")).to.equal(0);
            expect(utility.isNullOrUndefined(null)).to.equal(true);
            expect(utility.isNullOrUndefined(undefined)).to.equal(true);
            expect(utility.isNullOrUndefined(0)).to.equal(false);
            expect(utility.isNullOrUndefined("")).to.equal(false);
        });

        it("should convert maps to persisted objects", async () => {
            await utility.store(framework, "mappings", new Map<number, any[]>([[12, [{ result: { Value: "A" } }]]]), DataStoreLocation.Persistent);

            expect(storedValues).to.deep.equal([{
                identifier: "mappings",
                data: { 12: [{ result: { Value: "A" } }] },
                location: DataStoreLocation.Persistent
            }]);
        });
    });

    describe("resolveSmartTable", () => {
        const tableKeys = new Map<string, any>([["Resource", "ResourceA"]]);
        const resolveValues = new Map<string, any>([["Mode", "Automatic"]]);

        it("should use a matching persisted mapping without calling System", async () => {
            const hashCode = utility.createHashCode("ResourceResourceA");
            framework.dataStore.retrieve = async () => ({
                [hashCode]: [{ result: { Mode: "Automatic", Value: "Cached" } }]
            });

            const result = await utility.resolveSmartTable(framework, tableKeys, resolveValues, "mappings", "ConfigurationTable");

            expect(result).to.deep.equal({ Mode: "Automatic", Value: "Cached" });
            expect(systemCalls).to.have.lengthOf(0);
            expect(storedValues).to.have.lengthOf(0);
        });

        it("should resolve from System, cache the mappings, and persist them", async () => {
            framework.system.call = async (input: any) => {
                systemCalls.push(input);
                return {
                    Result: {
                        T_ST_ConfigurationTable: [
                            { Mode: "Automatic", Value: "Resolved" },
                            { Mode: "Manual", Value: "Other" }
                        ]
                    }
                };
            };

            const result = await utility.resolveSmartTable(framework, tableKeys, resolveValues, "mappings", "ConfigurationTable", true);

            expect(result).to.deep.equal({ Mode: "Automatic", Value: "Resolved" });
            expect(systemCalls).to.have.lengthOf(1);
            expect(systemCalls[0]).to.be.an.instanceof(System.LBOS.Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects.ResolveSmartTableInput);
            expect(systemCalls[0].SmartTable.Name).to.equal("ConfigurationTable");
            expect(systemCalls[0].Values).to.equal(tableKeys);
            expect(systemCalls[0].OnlyFirstRow).to.equal(true);
            expect(storedValues).to.have.lengthOf(1);
            expect(storedValues[0].identifier).to.equal("mappings");
            expect(storedValues[0].location).to.equal(DataStoreLocation.Persistent);
            expect(utility.mappings.size).to.equal(1);
        });

        it("should return undefined when System resolves no rows", async () => {
            framework.system.call = async (input: any) => {
                systemCalls.push(input);
                return { Result: { T_ST_ConfigurationTable: [] } };
            };

            const result = await utility.resolveSmartTable(framework, tableKeys, null, "mappings", "ConfigurationTable");

            expect(result).to.be.undefined;
            expect(systemCalls).to.have.lengthOf(1);
            expect(storedValues).to.have.lengthOf(0);
        });
    });

    describe("resetTableMapping", () => {
        it("should clear cached mappings and persist the empty collection", async () => {
            await utility.resetTableMapping(framework, "mappings");

            expect(utility.mappings).to.be.instanceof(Map);
            expect(utility.mappings.size).to.equal(0);
            expect(storedValues).to.deep.equal([{
                identifier: "mappings",
                data: {},
                location: DataStoreLocation.Persistent
            }]);
        });
    });
});