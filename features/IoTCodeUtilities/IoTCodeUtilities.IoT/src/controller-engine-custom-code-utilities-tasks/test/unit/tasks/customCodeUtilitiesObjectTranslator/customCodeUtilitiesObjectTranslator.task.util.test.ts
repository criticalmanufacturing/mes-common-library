import { expect } from "chai";

import {
    CustomUtilitiesUtilObjectTranslator,
    ID
} from "../../../../src/tasks/customCodeUtilitiesObjectTranslator/customCodeUtilitiesObjectTranslator.task.util";

describe("CustomUtilitiesUtilObjectTranslator tests", () => {
    let translator: CustomUtilitiesUtilObjectTranslator;

    beforeEach(() => {
        translator = new CustomUtilitiesUtilObjectTranslator();
    });

    describe("constants", () => {
        it("should export the expected implementation id", () => {
            expect(ID).to.equal("customCodeUtilitiesObjectTranslator");
        });
    });

    describe("anyToString", () => {
        it("should serialize dates, errors, maps, and plain objects", () => {
            const date = new Date("2024-01-02T03:04:05.000Z");
            const error = new Error("failure");
            const map = new Map<string, number>([["first", 1]]);

            expect(translator.anyToString(date, "fallback")).to.equal("2024-01-02T03:04:05.000Z");
            expect(translator.anyToString(error, "fallback")).to.equal(JSON.stringify(error, Object.getOwnPropertyNames(error)));
            expect(translator.anyToString(map, "fallback")).to.equal("[[\"first\",1]]");
            expect(translator.anyToString({ value: 1 }, "fallback")).to.equal("{\"value\":1}");
        });

        it("should return primitive values, serialize null, and use the fallback for undefined values", () => {
            expect(translator.anyToString(42, "fallback")).to.equal("42");
            expect(translator.anyToString(null, "fallback")).to.equal("null");
            expect(translator.anyToString(undefined, "fallback")).to.equal("fallback");
            expect(translator.anyToString(undefined, "")).to.equal("");
        });
    });

    describe("binary arithmetic", () => {
        it("should add equal-length binary values including a carry", () => {
            expect(translator.addBinary("1010", "0110")).to.equal("10000");
        });

        it("should pad the shorter binary value before adding", () => {
            expect(translator.addBinary("1", "111")).to.equal("1000");
        });
    });

    describe("base conversion", () => {
        it("should convert ASCII values to binary, hexadecimal, and decimal", () => {
            expect(translator.asciiToBinary("AB")).to.equal("0100000101000010");
            expect(translator.asciiToHex("AB")).to.equal("4142");
            expect(translator.asciiToDecimal("A")).to.equal(65);
        });

        it("should convert binary values to ASCII and decimal", () => {
            expect(translator.binaryToAscii("0100000101000010")).to.equal("AB");
            expect(translator.binaryToDecimal("101010")).to.equal(42);
        });

        it("should convert decimal values to ASCII, binary, and hexadecimal", () => {
            expect(translator.decimalToAscii(65)).to.equal("A");
            expect(translator.decimalToBinary(42)).to.equal("101010");
            expect(translator.decimalToHex(255)).to.equal("ff");
        });

        it("should convert hexadecimal values to ASCII and decimal", () => {
            expect(translator.hexToAscii("4869")).to.equal("Hi");
            expect(translator.hexToDecimal("2a")).to.equal(42);
        });
    });

    describe("padding", () => {
        it("should pad values to the requested size without truncating longer values", () => {
            expect(translator.padZeroesLeft("42", 4)).to.equal("0042");
            expect(translator.padZeroesRight("42", 4)).to.equal("4200");
            expect(translator.padZeroesLeft("12345", 2)).to.equal("12345");
            expect(translator.padZeroesRight("12345", 2)).to.equal("12345");
        });
    });

    describe("non-printable ASCII replacement", () => {
        it("should replace recognized token occurrences and preserve other text", () => {
            expect(translator.replaceNonPrintableASCIIChars_Tokens("A<NUL>B<DEL>C", "_")).to.equal("A_B_C");
            expect(translator.replaceNonPrintableASCIIChars_Tokens(null, "_")).to.be.null;
        });

        it("should replace recognized numeric values in token order and preserve other text", () => {
            expect(translator.replaceNonPrintableASCIIChars_Values("A0B127C", "_")).to.equal("A_B___C");
            expect(translator.replaceNonPrintableASCIIChars_Values(null, "_")).to.be.null;
        });
    });
});