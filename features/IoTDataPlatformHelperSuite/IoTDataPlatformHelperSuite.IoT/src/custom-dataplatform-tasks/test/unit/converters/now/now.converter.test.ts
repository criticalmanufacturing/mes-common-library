import "reflect-metadata";
import { Converter } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
// import * as chai from "chai";
import { NowConverter } from "../../../../src/converters/now/now.converter";

describe("Now converter", () => {

    let converter: Converter.ConverterContainer;

    beforeEach(async () => {
        converter = await EngineTestSuite.createConverter({
            class: NowConverter
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
