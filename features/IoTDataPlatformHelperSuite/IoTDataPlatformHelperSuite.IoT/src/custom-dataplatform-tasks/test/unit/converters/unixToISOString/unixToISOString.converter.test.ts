import "reflect-metadata";
import { Converter } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
// import * as chai from "chai";
import { UnixToISOStringConverter } from "../../../../src/converters/unixToISOString/unixToISOString.converter";

describe("Unix To ISO String converter", () => {

    let converter: Converter.ConverterContainer;

    beforeEach(async () => {
        converter = await EngineTestSuite.createConverter({
            class: UnixToISOStringConverter
        });
    });

    it("should convert", async (done) => {
        /* Example int to string
        let result: string = await converter.execute(123, {
            parameter: "something"
        });

        chai.expect(result).to.equal("123");
        */
        done();
    });

});
