import "reflect-metadata";
import * as chai from "chai";
import { MessageBusMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/message-bus.mock";
import { PyodideManagerHandler } from "../../../../src/lib/tasks/python-code/pyodide/pyodideManagerHandler";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const stubLogger    = { info: () => {}, warning: () => {}, error: () => {}, debug: () => {} };
const noopDataStore = { retrieve: async () => null, store: async () => {} };
const noopSystemApi = { call: async () => null };

// ─── Suite ────────────────────────────────────────────────────────────────────

describe("PythonCode API (message_bus) tests", function () {
    this.timeout(120_000);

    let manager: PyodideManagerHandler;
    let messageBus: MessageBusMock;

    before(async function () {
        messageBus = new MessageBusMock();

        manager = new PyodideManagerHandler();
        const ref = manager as unknown as Record<string, unknown>;
        ref["_logger"]     = stubLogger;
        ref["_dataStore"]  = noopDataStore;
        ref["_messageBus"] = messageBus;
        ref["_systemApi"]  = noopSystemApi;
        ref["_driver"]     = null;
        await manager.initialize({ packages: [], executionTimeoutMs: 0 });
    });

    after(async () => { await manager.destroy(); });

    beforeEach(() => {
        messageBus = new MessageBusMock();
        (manager as any)["_messageBus"] = messageBus;
    });

    // ─── publish ─────────────────────────────────────────────────────────────

    it("should deliver a published message to a JS subscriber", async () => {
        const received: unknown[] = [];
        messageBus.subscribe("py/topic", (_subject: string, body: unknown) => { received.push(body); });

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.mb = fw['message_bus']",
            "    async def main(self, inputs, outputs):",
            "        self.mb.publish('py/topic', 'hello')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(received).to.deep.equal(["hello"]);
    });

    it("should publish a numeric payload", async () => {
        const received: unknown[] = [];
        messageBus.subscribe("py/numbers", (_subject: string, body: unknown) => { received.push(body); });

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.mb = fw['message_bus']",
            "    async def main(self, inputs, outputs):",
            "        self.mb.publish('py/numbers', 42)",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(received).to.deep.equal([42]);
    });

    // ─── send_request ─────────────────────────────────────────────────────────

    it("should return the reply from send_request", async () => {
        messageBus.subscribe("py/req", (_subject: string, _body: unknown, reply: (v: unknown) => void) => {
            reply("world");
        });

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.mb = fw['message_bus']",
            "    async def main(self, inputs, outputs):",
            "        reply = await self.mb.send_request('py/req', 'hello')",
            "        return {'reply': reply}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["reply"]).to.equal("world");
    });

    it("should forward the request message to the JS subscriber", async () => {
        let receivedMsg: unknown;
        messageBus.subscribe("py/echo", (_subject: string, body: unknown, reply: (v: unknown) => void) => {
            receivedMsg = body;
            reply(null);
        });

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.mb = fw['message_bus']",
            "    async def main(self, inputs, outputs):",
            "        await self.mb.send_request('py/echo', 'ping')",
            "        return {}"
        ].join("\n");

        await (await manager.compile(code))({}, {});
        chai.expect(receivedMsg).to.equal("ping");
    });

    it("should return a numeric reply from send_request", async () => {
        messageBus.subscribe("py/num", (_subject: string, _body: unknown, reply: (v: unknown) => void) => {
            reply(99);
        });

        const code = [
            "class Code:",
            "    def __init__(self, fw):",
            "        self.mb = fw['message_bus']",
            "    async def main(self, inputs, outputs):",
            "        n = await self.mb.send_request('py/num', None)",
            "        return {'n': n}"
        ].join("\n");

        const result = await (await manager.compile(code))({}, {});
        chai.expect(result["n"]).to.equal(99);
    });
});
