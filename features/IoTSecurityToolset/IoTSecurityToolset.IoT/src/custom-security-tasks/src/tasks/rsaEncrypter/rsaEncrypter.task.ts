import { Task, System, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as crypto from "node:crypto";
import { Buffer } from "node:buffer";

/** Default values for settings */
export const SETTINGS_DEFAULTS: RsaEncrypterSettings = {
};

/**
 * @whatItDoes
 *
 * This task encrypts a string using the RSA public key stored in the data store by the RsaSetupTask. The encrypted value is emitted as a base64 string.
 *
 * @howToUse
 *
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 * * `string` : **value** - The plain text string to encrypt
 *
 * ### Outputs
 *
 * * `string` : **encryptedBase64** - The encrypted value as a base64 string
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see RsaEncrypterSettings}
 */
@Task.Task()
export class RsaEncrypterTask extends TaskBase {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    public publicKey: string = "";
    public value: string = "";

    /** **Outputs** */
    /** EncryptedBase64 */
    public encryptedBase64: Task.Output<string> = new Task.Output<string>();

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
                if (this.publicKey == null) {
                    this.publicKey = await this._dataStore.retrieve("rsa_publicKey", undefined);
                }

                this.encryptedBase64.emit(crypto.publicEncrypt(
                    { key: this.publicKey, padding: crypto.constants.RSA_PKCS1_OAEP_PADDING, oaepHash: 'sha256' },
                    Buffer.from(this.value, 'utf8')
                ).toString('base64'));
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
/** RsaEncrypter Settings object */
export interface RsaEncrypterSettings extends System.TaskDefaultSettings {
}
