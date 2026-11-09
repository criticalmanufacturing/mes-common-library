import { Task, System, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as crypto from "node:crypto";
import { Buffer } from "node:buffer";

/** Default values for settings */
export const SETTINGS_DEFAULTS: RsaDecrypterSettings = {
};

/**
 * @whatItDoes
 *
 * This task decrypts a base64 string using RSA decryption with a private key stored in the data store by the RsaEncrypterTask.
 *
 * @howToUse
 *
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 * * `string` : ** encryptedBase64 ** - The base64 string to be decrypted
 *
 * ### Outputs
 *
 * * `string` : ** value ** - The decrypted value
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see RsaDecrypterSettings}
 */
@Task.Task()
export class RsaDecrypterTask extends TaskBase {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    /** EncryptedBase64 */
    public encryptedBase64: string = "";

    /** **Outputs** */
    /** Value */
    public value: Task.Output<string> = new Task.Output<string>();

    /** Properties Settings */

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            // It is advised to reset the activate to allow being reactivated without the value being different
            this.activate = undefined;

            try {
                const buffer = Buffer.from(this.encryptedBase64, 'base64');
                const privatekey = await this._dataStore.retrieve("rsa_privateKey", undefined)
                const decrypted = crypto.privateDecrypt(
                    { key: privatekey, padding: crypto.constants.RSA_PKCS1_OAEP_PADDING, oaepHash: 'sha256' },
                    buffer
                ).toString('utf8');

                this.value.emit(decrypted);
                this.success.emit(true);
            } catch (error) {
                this.logAndEmitError((error as Error)?.message);
            }
        }
    }

    /** Right after settings are loaded, create the needed dynamic outputs. */
    public override async onBeforeInit(): Promise<void> {
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);
    }

    /** Cleanup internal data, unregister any event handler, etc */
    public override async onDestroy(): Promise<void> {
    }
}

// Add settings here
/** RsaDecrypter Settings object */
export interface RsaDecrypterSettings extends System.TaskDefaultSettings {
}
