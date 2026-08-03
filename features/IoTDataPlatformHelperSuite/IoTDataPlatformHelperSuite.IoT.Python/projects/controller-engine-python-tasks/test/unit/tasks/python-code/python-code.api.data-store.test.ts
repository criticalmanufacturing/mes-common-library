import "reflect-metadata";
import * as chai from "chai";
import { System } from "@criticalmanufacturing/connect-iot-controller-engine";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
import { PyodideManagerHandler } from "../../../../src/lib/tasks/python-code/pyodide/pyodideManagerHandler";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const stubLogger     = { info: () => {}, warning: () => {}, error: () => {}, debug: () => {} };
const noopMessageBus = { sendRequest: async () => null, publish: () => {} };
const noopSystemApi  = { call: async () => null };

// ─── Suite ────────────────────────────────────────────────────────────────────

describe("PythonCode API (data_store) tests", function () {
    this.timeout(120_000);

    let manager: PyodideManagerHandler;
    let dataStore: DataStoreMock;

    before(async function () {
        dataStore = new DataStoreMock();

        manager = new PyodideManagerHandler();
        const ref = manager as unknown as Record<string, unknown>;
        ref["_logger"]    = stubLogger;
        ref["_dataStore"] = dataStore;
        ref["_messageBus"] = noopMessageBus;
        ref["_systemApi"] = noopSystemApi;
        ref["_driver"]    = null;
        await manager.initialize({ packages: [], executionTimeoutMs: 0 });
    });

    after(async () => { await manager.destroy(); });

    beforeEach(() => { dataStore = new DataStoreMock(); (manager as any)["_dataStore"] = dataStore; });

    // ─── get ─────────────────────────────────────────────────────────────────

    it("should retrieve a previously stored temporary value", async () => {
        dataStore.set("key", 42, System.DataStoreLocation.Temporary);

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        return {'value': await self.ds.get('key')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["value"]).to.equal(42);
    });

    it("should return None when key does not exist", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        v = await self.ds.get('missing')",
            "        return {'is_none': v is None}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["is_none"]).to.equal(true);
    });

    it("should retrieve a string value", async () => {
        dataStore.set("name", "hello", System.DataStoreLocation.Temporary);

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        return {'value': await self.ds.get('name')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["value"]).to.equal("hello");
    });

    // ─── set ─────────────────────────────────────────────────────────────────

    it("should store a value and retrieve it in the same execution", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        await self.ds.set('counter', 7)",
            "        return {'value': await self.ds.get('counter')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["value"]).to.equal(7);
    });

    it("should persist the stored value to the JS DataStore", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        await self.ds.set('result', 99)",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(dataStore.get("result", System.DataStoreLocation.Temporary)).to.equal(99);
    });

    it("should overwrite an existing value", async () => {
        dataStore.set("x", 1, System.DataStoreLocation.Temporary);

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        await self.ds.set('x', 100)",
            "        return {'value': await self.ds.get('x')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["value"]).to.equal(100);
        chai.expect(dataStore.get("x", System.DataStoreLocation.Temporary)).to.equal(100);
    });

    it("should accumulate state across multiple activations with the same compiled function", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.ds = fw['data_store']",
            "    async def main(self, inputs, outputs):",
            "        prev = await self.ds.get('n') or 0",
            "        await self.ds.set('n', prev + 1)",
            "        return {'n': await self.ds.get('n')}"
        ].join("\n");

        const fn = await manager.compile(code);
        const r1 = await fn({}, {});
        const r2 = await fn({}, {});
        const r3 = await fn({}, {});
        chai.expect(r1["n"]).to.equal(1);
        chai.expect(r2["n"]).to.equal(2);
        chai.expect(r3["n"]).to.equal(3);
    });
});
