import { ConnectIoTPackageMetadata } from 'cmf-core-connect-iot/extensions';

export const Metadata: ConnectIoTPackageMetadata = {
    name: '@criticalmanufacturing/connect-iot-controller-engine-codecsharp-tasks',
    friendlyName: 'CSharp Code Tasks',
    version: '0.0.0',
    load: () => import('@criticalmanufacturing/connect-iot-controller-engine-codecsharp-tasks'),
    tasks: [
        'cSharpRoslynCode'
    ],
    converters: [],
    fonts: []
};