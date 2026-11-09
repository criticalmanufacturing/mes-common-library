import "reflect-metadata";
import * as chai from "chai";
import { PyodideManagerHandler } from "../../../../src/lib/tasks/python-code/pyodide/pyodideManagerHandler";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const stubLogger    = { info: () => {}, warning: () => {}, error: () => {}, debug: () => {} };
const noopDataStore = { retrieve: async () => null, store: async () => {} };
const noopMessageBus = { sendRequest: async () => null, publish: () => {} };
const noopSystemApi  = { call: async () => null };

// ─── Suite ────────────────────────────────────────────────────────────────────

describe("PythonCode API (utils) tests", function () {
    this.timeout(120_000);

    let manager: PyodideManagerHandler;

    before(async function () {
        manager = new PyodideManagerHandler();
        const ref = manager as unknown as Record<string, unknown>;
        ref["_logger"]     = stubLogger;
        ref["_dataStore"]  = noopDataStore;
        ref["_messageBus"] = noopMessageBus;
        ref["_systemApi"]  = noopSystemApi;
        ref["_driver"]     = null;
        await manager.initialize({ packages: [], executionTimeoutMs: 0 });
    });

    after(async () => { await manager.destroy(); });

    // ─── sleep ───────────────────────────────────────────────────────────────

    it("should complete sleep without error", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def main(self, inputs, outputs):",
            "        await self.utils.sleep(10)",
            "        return {'done': True}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["done"]).to.equal(true);
    });

    // ─── convert_value_to_type ───────────────────────────────────────────────

    it("should convert a value to String", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def main(self, inputs, outputs):",
            "        return {'result': self.utils.convert_value_to_type(42, 'String')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal("42");
    });

    it("should convert a string to Integer", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def main(self, inputs, outputs):",
            "        return {'result': self.utils.convert_value_to_type('7', 'Integer')}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal(7);
    });

    it("should convert to Boolean (truthy values)", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def main(self, inputs, outputs):",
            "        return {",
            "            'from_1':   self.utils.convert_value_to_type('1',   'Boolean'),",
            "            'from_yes': self.utils.convert_value_to_type('yes', 'Boolean'),",
            "            'from_0':   self.utils.convert_value_to_type('0',   'Boolean'),",
            "        }"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["from_1"]).to.equal(true);
        chai.expect(result["from_yes"]).to.equal(true);
        chai.expect(result["from_0"]).to.equal(false);
    });

    it("should return the default value when conversion is not possible and throwOnError is False", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def main(self, inputs, outputs):",
            "        return {'result': self.utils.convert_value_to_type('abc', 'Integer', -1, False)}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal(-1);
    });

    // ─── execute_with_retry ──────────────────────────────────────────────────

    it("should return the result when the function succeeds on first try", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def _fn(self):",
            "        return 'ok'",
            "    async def main(self, inputs, outputs):",
            "        result = await self.utils.execute_with_retry(None, 3, 0, self._fn)",
            "        return {'result': result}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal("ok");
    });

    it("should retry the function and succeed on the last allowed attempt", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "        self.attempts = 0",
            "    async def _failing_fn(self):",
            "        self.attempts += 1",
            "        if self.attempts < 3:",
            "            raise Exception('not yet')",
            "        return self.attempts",
            "    async def main(self, inputs, outputs):",
            "        result = await self.utils.execute_with_retry(None, 3, 0, self._failing_fn)",
            "        return {'result': result, 'attempts': self.attempts}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal(3);
        chai.expect(result["attempts"]).to.equal(3);
    });

    // ─── execute_with_system_error_retry ─────────────────────────────────────

    it("should return the result when execute_with_system_error_retry succeeds on first try", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.utils = fw['utils']",
            "    async def _fn(self):",
            "        return 'done'",
            "    async def main(self, inputs, outputs):",
            "        result = await self.utils.execute_with_system_error_retry(None, 3, 0, self._fn)",
            "        return {'result': result}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal("done");
    });
});
