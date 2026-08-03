// import "reflect-metadata";
// import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
// import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
// // import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
// import * as chai from "chai";

// import {
//     RsaEncryptorTask,
//     RsaEncryptorSettings
// } from "../../../../src/tasks/rsaEncrypter/rsaEncrypter.task";

// describe("RsaEncryptor Task tests", () => {


//     // Optional: See container handling under rsaEncryptorTestFactory
//     // let dataStoreMock: DataStoreMock;
//     beforeEach(() => {
//         // dataStoreMock = new DataStoreMock();
//     });

// 	// eslint-disable-next-line @typescript-eslint/ban-types
//     const rsaEncryptorTestFactory = (settings: RsaEncryptorSettings | undefined, trigger: Function, validate: Function): void => {

//         const taskDefinition = {
//             class: RsaEncryptorTask,
//             id: "0",
//             settings: (settings || {
// 				example: "Hello World",
//             } as RsaEncryptorSettings)
//         };

//         EngineTestSuite.createTasks([
//             taskDefinition,
//             {
//                 id: "1",
//                 class: Task.Task({
//                     name: "mockTask"
//                 })(class MockTask implements Task.TaskInstance {
//                     [key: string]: any;
//                     _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

//                     async onBeforeInit(): Promise<void> {
//                         this["activate"] = new Task.Output<any>();
//                         this._outputs.set("activate", this["activate"]);
//                         // Create other custom outputs (for the Mock task) here
//                     }

//                     // Trigger the test
//                     async onInit(): Promise<void> {
//                         trigger(this._outputs);
//                     }

//                     // Validate the results
//                     async onChanges(changes: Task.Changes): Promise<void> {
//                         validate(changes);
//                     }
//                 })
//             }
//         ], [
//             { sourceId: "1", outputName: `activate`, targetId: "0", inputName: "activate", },
//             { sourceId: "0", outputName: `success`, targetId: "1", inputName: "success", },
//             { sourceId: "0", outputName: `error`, targetId: "1", inputName: "error", },
//             // Add more links needed here...
//         ],
//         undefined,
//         (containerId) => {
//             // Change what you need in the container
//             // Example:
//             // containerId.unbind(TYPES.System.PersistedDataStore);
//             // containerId.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);
//         });
//     };

//     /**
//      * Instructions about the tests
//      * It is assumed that there are two tasks:
//      *    0 - RsaEncryptor Task
//      *    1 - Mockup task
//      *
//      * All Outputs of Mock task are connected to the inputs of the RsaEncryptor task
//      * All Outputs of RsaEncryptor Task are connected to the Mock task inputs
//      *
//      * You, as the tester developer, will trigger the outputs necessary for the RsaEncryptor to be activated
//      * and check the changes to see if the RsaEncryptor task sent you the correct values
//      *
//      * Note: This is just an example about how to unit test the task. Not mandatory to use this method!
//      */

//     it("should get success when activated", (done) => {
//         rsaEncryptorTestFactory(undefined,
//             (outputs: Map<string, Task.Output<any>>) => {
//                 // Trigger an output
//                 outputs.get("activate").emit(true);
//             }, (changes: Task.Changes) => {
//                 // Validate the input
//                 chai.expect(changes["success"].currentValue).to.equal(true);
//                 // Report the test as a success
//                 done();
//             });
//     });
// });
