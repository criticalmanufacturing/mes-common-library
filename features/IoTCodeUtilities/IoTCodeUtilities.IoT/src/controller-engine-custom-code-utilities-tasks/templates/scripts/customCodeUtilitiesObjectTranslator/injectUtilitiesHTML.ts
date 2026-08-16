import type { IoTATLScriptContextTest } from '../../types';

export function injectUtilitiesHTML(): IoTATLScriptContextTest {
    return {
        _execute: async function () {
            // PackagePacker: Start of Async Script
            const ID: string = "customCodeUtilitiesObjectTranslator"
            const UTIL_API_DTS_CONTENT: string = `
/**
 * Example task shared code library, to be used within code task
 */
export interface CustomUtilitiesUtilObjectTranslator {
    anyToString(value: any, defaultValue: string): string;
    addBinary(a: string, b: string): string;
    asciiToBinary(input: string): string;
    asciiToHex(input: string): string;
    asciiToDecimal(input: string): number;
    binaryToAscii(input: string): string;
    binaryToDecimal(input: string): number;
    decimalToAscii(input: number): string;
    decimalToBinary(input: number): string;
    decimalToHex(input: number): string;
    hexToAscii(input: string): string;
    hexToDecimal(input: string): number;
    padZeroesLeft(value: string, size: number): string;
    padZeroesRight(value: string, size: number): string;
    replaceNonPrintableASCIIChars_Tokens(value: string, replaceWith: string): string;
    replaceNonPrintableASCIIChars_Values(value: string, replaceWith: string): string;
}`;
            const UTIL_API_CLASS_NAME: string = "customUtilitiesObjectTranslator";
            this.service?.container.library.addFields(
                { name: ID, type: UTIL_API_CLASS_NAME }
            );
            this.service?.container.library.addDefinitions(
                UTIL_API_DTS_CONTENT
            );

            // PackagePacker: End of Async Script
        },
    };
}
