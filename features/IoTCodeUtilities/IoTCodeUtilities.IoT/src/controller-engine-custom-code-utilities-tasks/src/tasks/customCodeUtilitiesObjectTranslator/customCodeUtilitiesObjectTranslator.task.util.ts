/**
 * Example task shared code library, to be used within code task
 */
export class CustomUtilitiesUtilObjectTranslator {

    private _NonPrintableASCIIChars_Tokens: string[] = [
        "<NUL>", "<SOH>", "<STX>", "<ETX>", "<EOT>", "<ENQ>", "<ACK>", "<BEL>", "<BS>", "<TAB>",
        "<LF>", "<VT>", "<FF>", "<CR>", "<SO>", "<SI>", "<DLE>", "<DC1>", "<DC2>", "<DC3>",
        "<DC4>", "<NAK>", "<SYN>", "<ETB>", "<CAN>", "<EM>", "<SUB>", "<ESC>", "<FS>", "<GS>",
        "<RS>", "<US>", "<DEL>"
    ];
    private _NonPrintableASCIIChars_Values: number[] = [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
        20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
        30, 31, 127
    ];

    private _currentMappings: Map<number, any[]>;

    public get mappings(): Map<number, any[]> { return (this._currentMappings); }

    /**
     * Transforms Any Object to a string
     */
    public anyToString(value: any, defaultValue: string): string {
        if (typeof value === "object") {
            if (value instanceof Date) {
                return (value.toISOString());
            } else if (value instanceof Error) {
                return (JSON.stringify(value, Object.getOwnPropertyNames(value)));
            } else if (value instanceof Map) {
                return (JSON.stringify(Array.from(value.entries())));
            } else {
                return (JSON.stringify(value));
            }
        } else {
            if (value == null) {
                return (defaultValue || "");
            } else {
                return typeof value["toString"] === "function" ? value.toString() : defaultValue || "";
            }
        }
    }

    /**
     * Adds two binary values
     */
    public addBinary(a: string, b: string): string {

        let sum = "";
        let carry = "";

        const paddedInput = this.padZeroes(a, b);
        a = paddedInput[0];
        b = paddedInput[1];

        for (let i = a.length - 1; i >= 0; i--) {
            if (i === a.length - 1) {
                // half add the first pair
                const halfAdd1 = this.halfAdder(a.charAt(i), b.charAt(i));
                sum = halfAdd1[0].toString() + sum.toString();
                carry = halfAdd1[1].toString();
            } else {
                // full add the rest
                const fullAdd = this.fullAdder(a.charAt(i), b.charAt(i), carry);
                sum = fullAdd[0] + sum;
                carry = fullAdd[1];
            }
        }

        return carry ? carry + sum : sum;
    }

    /**
     * Coverts strings to string of binary
     */
    public asciiToBinary(input: string): string {
        const characters = input.split('');

        return characters.map(function (char) {
            const binary = char.charCodeAt(0).toString(2)
            const pad = Math.max(8 - binary.length, 0);
            // Just to make sure it is 8 bits long.
            return '0'.repeat(pad) + binary;
        }).join('');
    }

    /**
     * Coverts strings to hexadecimal string
     */
    public asciiToHex(input: string): string {

        let result = '';
        for (let i = 0; i < input.length; i++) {
            result += input.charCodeAt(i).toString(16);
        }
        return result;
    }

    /**
     * Coverts ascii to decimal
     */
    public asciiToDecimal(input: string): number {
        return Number(input.charCodeAt(0).toString(10));
    }

    /**
     * Coverts binary string to ascii
     */
    public binaryToAscii(input: string): string {
        let bytesLeft = input;
        let result = '';

        // Check if we have some bytes left
        while (bytesLeft.length) {
            // Get the first digits
            const byte = bytesLeft.substr(0, 8);
            bytesLeft = bytesLeft.substr(8);

            result += String.fromCharCode(parseInt(byte, 2));
        }

        return result;
    }

    /**
     * Coverts binary to decimal
     */
    public binaryToDecimal(input: string): number {
        return parseInt(input, 2);
    }

    /**
     * Coverts decimal to ascii string
     */
    public decimalToAscii(input: number): string {
        return String.fromCharCode(input);
    }

    /**
     * Coverts decimal to binary string
     */
    public decimalToBinary(input: number): string {
        return input.toString(2);
    }

    /**
     * Coverts decimal to Hexadecimal string
     */
    public decimalToHex(input: number): string {
        return input.toString(16);
    }

    /**
     * Coverts strings to hexadecimal string
     */
    public hexToAscii(input: string): string {
        return Buffer.from(input, 'hex').toString();
    }

    /**
     * Coverts Hexadecimal to decimal
     */
    public hexToDecimal(input: string): number {
        return parseInt(input, 16);
    }

    /**
     * Adds Zeroes to the left until size is met
     */
    public padZeroesLeft(value: string, size: number): string {
        const lengthDifference = size - value.length;
        switch (lengthDifference) {
            case 0:
                break;
            default:
                const zeroes = Array.from(Array(Math.abs(lengthDifference)), () => String(0));
                if (lengthDifference > 0) {
                    value = `${zeroes.join('')}${value}`;
                }
        }
        return value;
    }

    /**
     * Adds Zeroes to the right until size is met
     */
    public padZeroesRight(value: string, size: number): string {
        const lengthDifference = size - value.length;
        switch (lengthDifference) {
            case 0:
                break;
            default:
                const zeroes = Array.from(Array(Math.abs(lengthDifference)), () => String(0));
                if (lengthDifference > 0) {
                    value = `${value}${zeroes.join('')}`;
                }
        }
        return value;
    }

    /**
     * Replaces NonPrintableASCIIChars_Tokens with a specific value "<NUL>", "<SOH>", "<STX>", "<ETX>", "<EOT>", "<ENQ>", "<ACK>", "<BEL>", "<BS>", "<TAB>",
        "<LF>", "<VT>", "<FF>", "<CR>", "<SO>", "<SI>", "<DLE>", "<DC1>", "<DC2>", "<DC3>",
        "<DC4>", "<NAK>", "<SYN>", "<ETB>", "<CAN>", "<EM>", "<SUB>", "<ESC>", "<FS>", "<GS>",
        "<RS>", "<US>", "<DEL>"
     */
    public replaceNonPrintableASCIIChars_Tokens(value: string, replaceWith: string): string {

        if (this._NonPrintableASCIIChars_Tokens.length !== this._NonPrintableASCIIChars_Values.length) {
            throw (new Error("Error on ASCII Non Printable Chars Token/Values Length."));
        } else {
            if (value != null) {
                for (let i = 0; i < this._NonPrintableASCIIChars_Tokens.length; i++) {
                    if (value.includes(this._NonPrintableASCIIChars_Tokens[i])) {
                        value = value.replace(this._NonPrintableASCIIChars_Tokens[i], replaceWith);
                    }
                }
            }
        }
        return value;
    }

    /**
     * Replaces NonPrintableASCIIChars_Tokens with a specific value
     *  0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
        20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
        30, 31, 127
     */
    public replaceNonPrintableASCIIChars_Values(value: string, replaceWith: string): string {

        if (this._NonPrintableASCIIChars_Values.length !== this._NonPrintableASCIIChars_Values.length) {
            throw (new Error("Error on ASCII Non Printable Chars Token/Values Length."));
        } else {
            if (value != null) {
                for (let i = 0; i < this._NonPrintableASCIIChars_Values.length; i++) {
                    if (value.includes(this._NonPrintableASCIIChars_Values[i].toString())) {
                        value = value.replace(this._NonPrintableASCIIChars_Values[i].toString(), replaceWith);
                    }
                }
            }
        }
        return value;
    }

    private halfAdder(a: string, b: string) {
        const sum = this.xor(a, b);
        const carry = this.and(a, b);
        return [sum, carry];
    }

    private fullAdder(a: string, b: string, carry: string) {
        const halfAdd = this.halfAdder(a, b);
        const sum = this.xor(carry, halfAdd[0]);
        carry = this.and(carry, halfAdd[0]);
        carry = this.or(carry, halfAdd[1]);

        return [sum, carry];
    }

    private xor(a: string, b: string): string { return (a === b ? "0" : "1"); }

    private and(a: string, b: string): string { return a === "1" && b === "1" ? "1" : "0"; }

    private or(a: string, b: string): string { return (a === "1" || b === "1") ? "1" : "0"; }

    private padZeroes(a: string, b: string): [string, string] {
        const lengthDifference = a.length - b.length;
        switch (lengthDifference) {
            case 0:
                break;
            default:
                const zeroes = Array.from(Array(Math.abs(lengthDifference)), () => String(0));
                if (lengthDifference > 0) {
                    // if a is longer than b
                    // then we pad b with zeroes
                    b = `${zeroes.join('')}${b}`;
                } else {
                    // if b is longer than a
                    // then we pad a with zeroes
                    a = `${zeroes.join('')}${a}`;
                }
        }
        return [a, b];
    }
}

export const ID: string = "customCodeUtilitiesObjectTranslator";
