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
export interface CustomCodeUtilitiesObjectTranslator {
    /** Converts a value to a string, using the default for null or unsupported values. */
    anyToString(value: any, defaultValue: string): string;
    /** Adds two binary strings and returns the binary result. */
    addBinary(a: string, b: string): string;
    /** Converts a string to its concatenated 8-bit binary representation. */
    asciiToBinary(input: string): string;
    /** Converts a string to its concatenated hexadecimal representation. */
    asciiToHex(input: string): string;
    /** Converts the first character of a string to its decimal character code. */
    asciiToDecimal(input: string): number;
    /** Converts a binary string to text by reading it in 8-bit groups. */
    binaryToAscii(input: string): string;
    /** Converts a binary string to a decimal number. */
    binaryToDecimal(input: string): number;
    /** Converts a decimal character code to a character. */
    decimalToAscii(input: number): string;
    /** Converts a decimal number to a binary string. */
    decimalToBinary(input: number): string;
    /** Converts a decimal number to a hexadecimal string. */
    decimalToHex(input: number): string;
    /** Converts a hexadecimal string to text. */
    hexToAscii(input: string): string;
    /** Converts a hexadecimal string to a decimal number. */
    hexToDecimal(input: string): number;
    /** Adds zeroes to the left until the string reaches the requested size. */
    padZeroesLeft(value: string, size: number): string;
    /** Adds zeroes to the right until the string reaches the requested size. */
    padZeroesRight(value: string, size: number): string;
    /** Replaces the first occurrence of each non-printable ASCII token. */
    replaceNonPrintableASCIIChars_Tokens(value: string, replaceWith: string): string;
    /** Replaces the first occurrence of each non-printable ASCII code value. */
    replaceNonPrintableASCIIChars_Values(value: string, replaceWith: string): string;
}`;
            const UTIL_API_CLASS_NAME: string = "CustomCodeUtilitiesObjectTranslator";
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
