import "reflect-metadata";
import * as chai from "chai";
import { PyodideManagerHandler } from "../../../../src/lib/tasks/python-code/pyodide/pyodideManagerHandler";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const noopDataStore  = { retrieve: async () => null, store: async () => {} };
const noopMessageBus = { sendRequest: async () => null, publish: () => {} };
const noopSystemApi  = { call: async () => null };

// ─── Suite ────────────────────────────────────────────────────────────────────

describe("PythonCode API (logger) tests", function () {
    this.timeout(120_000);

    let manager: PyodideManagerHandler;

    const logged: { level: string; msg: string }[] = [];

    const trackingLogger = {
        info:    (m: string) => logged.push({ level: "info",    msg: m }),
        warning: (m: string) => logged.push({ level: "warning", msg: m }),
        error:   (m: string) => logged.push({ level: "error",   msg: m }),
        debug:   (m: string) => logged.push({ level: "debug",   msg: m }),
    };

    before(async function () {
        manager = new PyodideManagerHandler();
        const ref = manager as unknown as Record<string, unknown>;
        ref["_logger"]    = trackingLogger;
        ref["_dataStore"] = noopDataStore;
        ref["_messageBus"] = noopMessageBus;
        ref["_systemApi"] = noopSystemApi;
        ref["_driver"]    = null;
        await manager.initialize({ packages: [], executionTimeoutMs: 0 });
    });

    after(async () => { await manager.destroy(); });

    beforeEach(() => { logged.length = 0; });

    it("should forward logger.info to the JS logger", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.info('hello info')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged).to.deep.include({ level: "info", msg: "hello info" });
    });

    it("should forward logger.warning to the JS logger", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.warning('careful')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged).to.deep.include({ level: "warning", msg: "careful" });
    });

    it("should forward logger.error to the JS logger", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.error('something broke')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged).to.deep.include({ level: "error", msg: "something broke" });
    });

    it("should forward logger.debug to the JS logger", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.debug('trace point')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged).to.deep.include({ level: "debug", msg: "trace point" });
    });

    it("should coerce non-string messages to strings", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.info(42)",
            "        self.log.info([1, 2, 3])",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged).to.deep.include({ level: "info", msg: "42" });
        chai.expect(logged).to.deep.include({ level: "info", msg: "[1, 2, 3]" });
    });

    it("should log multiple messages in order", async () => {
        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.log = fw['logger']",
            "    async def main(self, inputs, outputs):",
            "        self.log.debug('first')",
            "        self.log.info('second')",
            "        self.log.warning('third')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(logged.map(l => l.msg)).to.deep.equal(["first", "second", "third"]);
    });
});
