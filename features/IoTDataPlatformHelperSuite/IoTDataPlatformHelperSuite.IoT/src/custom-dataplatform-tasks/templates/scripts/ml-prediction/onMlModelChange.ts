import { Task } from '@criticalmanufacturing/connect-iot-controller-engine';
import type { IoTATLScriptContextTest } from '../../types';
import Cmf from 'cmf-lbos';

export function onInit(): IoTATLScriptContextTest {
    return {
        _execute: async function () {
            // PackagePacker: Start of Async Script
            const inputs: Array<Task.TaskInput & { friendlyName: string }> = [];
            const outputs: Array<Task.TaskOutput & { friendlyName: string }> = [];

            if (this.settings.mlModel != null) {
                const loadMLModelInput =
                    new this.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects.GetObjectByIdInput();
                loadMLModelInput['Id'] = this.settings.mlModel.Id;
                loadMLModelInput['Type'] = 'MLModel';
                loadMLModelInput['LevelsToLoad'] = 0;
                loadMLModelInput['IgnoreLastServiceId'] = true;

                this.settings.mlModel = (
                    (await this.System.call(
                        loadMLModelInput
                    )) as Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects.GetObjectByIdOutput
                ).Instance;

                const loadFeaturesInput =
                    new this.Cmf.Foundation.BusinessOrchestration.DataPlatform.InputObjects.LoadMLModelFeaturesTransformationsInput();
                loadFeaturesInput.MLModel = this.settings.mlModel;
                loadFeaturesInput.IgnoreLastServiceId = true;

                const loadFeaturesOutput = (await this.System.call(
                    loadFeaturesInput
                )) as Cmf.Foundation.BusinessOrchestration.DataPlatform.OutputObjects.LoadMLModelFeaturesTransformationsOutput;

                const features: Cmf.Foundation.BusinessObjects.MLModelFeature[] =
                    loadFeaturesOutput.MLModelFeatures.filter(
                        (feature) =>
                            (feature.Enabled === true ||
                                feature.InUse === true) &&
                            feature.IsLabel === false &&
                            feature.Script == null
                    );

                this.settings.mlModel.Features = features;

                inputs.push({
                    name: 'eventId',
                    valueType: this.engineUtilities.convertToValueTypeObject(
                        'String',
                        'eventId'
                    ),
                    friendlyName: 'eventId',
                });

                features.forEach((feature) => {
                    inputs.push({
                        name: feature.Name,
                        valueType:
                            this.engineUtilities.convertMlTypeToComplexType(
                                feature.FeatureType,
                                feature.Name
                            ) as any,
                        friendlyName: feature.Name,
                    });
                });

                switch (this.settings.mlModel.MLModelType) {
                    case this.Cmf.DataPlatform.Shared.ModelType
                        .ClassificationBinary:
                        outputs.push({
                            name: 'prediction',
                            valueType:
                                this.engineUtilities.convertToValueTypeObject(
                                    'Boolean',
                                    'prediction'
                                ),
                            friendlyName: 'prediction',
                        });
                        break;
                    case this.Cmf.DataPlatform.Shared.ModelType
                        .ClassificationMulti:
                        outputs.push({
                            name: 'prediction',
                            valueType:
                                this.engineUtilities.convertToValueTypeObject(
                                    'String',
                                    'prediction'
                                ),
                            friendlyName: 'prediction',
                        });
                        break;
                    case this.Cmf.DataPlatform.Shared.ModelType.Regression:
                        outputs.push({
                            name: 'prediction',
                            valueType:
                                this.engineUtilities.convertToValueTypeObject(
                                    'Decimal',
                                    'prediction'
                                ),
                            friendlyName: 'prediction',
                        });
                        break;
                    case this.Cmf.DataPlatform.Shared.ModelType.Unsupervised:
                        outputs.push({
                            name: 'prediction',
                            valueType:
                                this.engineUtilities.convertToValueTypeObject(
                                    'Boolean',
                                    'prediction'
                                ),
                            friendlyName: 'prediction',
                        });

                        outputs.push({
                            name: 'score',
                            valueType:
                                this.engineUtilities.convertToValueTypeObject(
                                    'Decimal',
                                    'score'
                                ),
                            friendlyName: 'score',
                        });
                        break;
                }
            }

            this.settings.inputs = inputs;
            this.settings.outputs = outputs;
            this.reloadTree();
            // PackagePacker: End of Async Script
        },
    };
}