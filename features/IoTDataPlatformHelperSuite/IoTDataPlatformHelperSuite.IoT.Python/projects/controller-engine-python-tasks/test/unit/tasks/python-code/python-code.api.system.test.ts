import "reflect-metadata";
import * as chai from "chai";
import { PyodideManagerHandler } from "../../../../src/lib/tasks/python-code/pyodide/pyodideManagerHandler";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const stubLogger    = { info: () => {}, warning: () => {}, error: () => {}, debug: () => {} };
const noopDataStore = { retrieve: async () => null, store: async () => {} };
const noopMessageBus = { sendRequest: async () => null, publish: () => {} };

// ─── Suite ────────────────────────────────────────────────────────────────────

describe("PythonCode API (system) tests", function () {
    this.timeout(120_000);

    let manager: PyodideManagerHandler;

    let lastInput: unknown;
    let stubbedResponse: unknown;

    const systemStub = {
        call: async (input: unknown) => {
            lastInput = input;
            return stubbedResponse;
        }
    };

    before(async function () {
        manager = new PyodideManagerHandler();
        const ref = manager as unknown as Record<string, unknown>;
        ref["_logger"]     = stubLogger;
        ref["_dataStore"]  = noopDataStore;
        ref["_messageBus"] = noopMessageBus;
        ref["_systemApi"]  = systemStub;
        ref["_driver"]     = null;
        await manager.initialize({ packages: [], executionTimeoutMs: 0 });
    });

    after(async () => { await manager.destroy(); });

    beforeEach(() => {
        lastInput       = undefined;
        stubbedResponse = null;
    });

    // ─── call ────────────────────────────────────────────────────────────────

    it("should call the system API and succeed without error", async () => {
        stubbedResponse = null;

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.sys = fw['system']",
            "    async def main(self, inputs, outputs):",
            "        await self.sys.call({'name': 'test'})",
            "        return {'called': True}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["called"]).to.equal(true);
    });

    it("should forward the Python dict as a JS object to the system API", async () => {
        stubbedResponse = null;

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.sys = fw['system']",
            "    async def main(self, inputs, outputs):",
            "        await self.sys.call({'action': 'doSomething', 'value': 123})",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(lastInput).to.not.be.null;
        chai.expect((lastInput as any).action).to.equal("doSomething");
        chai.expect((lastInput as any).value).to.equal(123);
    });

    it("should return a scalar response from the system call", async () => {
        stubbedResponse = 42;

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.sys = fw['system']",
            "    async def main(self, inputs, outputs):",
            "        result = await self.sys.call({})",
            "        return {'result': result}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["result"]).to.equal(42);
    });

    it("should surface a system API error as a Python exception", async () => {
        systemStub.call = async () => { throw new Error("system unavailable"); };

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.sys = fw['system']",
            "    async def main(self, inputs, outputs):",
            "        try:",
            "            await self.sys.call({})",
            "            return {'threw': False}",
            "        except Exception as e:",
            "            return {'threw': True, 'msg': str(e)}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["threw"]).to.equal(true);
        chai.expect(String(result["msg"])).to.include("system unavailable");

        // restore
        systemStub.call = async (input: unknown) => { lastInput = input; return stubbedResponse; };
    });

    it("should forward inputs from the task to the system call payload", async () => {
        stubbedResponse = null;

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.sys = fw['system']",
            "    async def main(self, inputs, outputs):",
            "        await self.sys.call({'container': inputs['name']})",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({ name: "CTR001" }, {});
        chai.expect((lastInput as any).container).to.equal("CTR001");
    });
});
