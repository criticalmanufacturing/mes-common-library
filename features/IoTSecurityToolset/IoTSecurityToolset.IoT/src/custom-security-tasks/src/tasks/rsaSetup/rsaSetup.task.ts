import { Task, System, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as crypto from "node:crypto";

/** Default values for settings */
export const SETTINGS_DEFAULTS: RsaSetupSettings = {
};

/**
 * @whatItDoes
 *
 * This task generates an RSA key pair and stores the private key in the data store for later use by the RsaDecrypterTask. The public key is emitted as output.
 *
 * @howToUse
 *
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 *
 * * `string` : ** publicKey ** - The generated public key in PEM format
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see RsaSetupSettings}
 */
@Task.Task()
export class RsaSetupTask extends TaskBase {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */

    /** **Outputs** */
    /** PublicKey */
    public publicKey: Task.Output<string> = new Task.Output<string>();

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
                const privateKey = await this._dataStore.retrieve("rsa_privateKey", undefined);
                if (!privateKey) {
                    const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', {
                        modulusLength: 2048,
                        publicKeyEncoding: { type: 'spki', format: 'pem' },
                        privateKeyEncoding: { type: 'pkcs8', format: 'pem' }
                    });
                    await this._dataStore.store("rsa_privateKey", privateKey, System.DataStoreLocation.Persistent);
                    await this._dataStore.store("rsa_publicKey", publicKey, System.DataStoreLocation.Persistent);
                }

                this.publicKey.emit(await this._dataStore.retrieve("rsa_publicKey", undefined));
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
/** RsaSetup Settings object */
export interface RsaSetupSettings extends System.TaskDefaultSettings {
}
