import type { IoTATLScriptContext } from "cmf-core-connect-iot";

type Prettify<T> = {
    [K in keyof T]: T[K];
} & object;

export type IoTATLScriptContextTest<T = Record<string, any>> = Prettify<Partial<IoTATLScriptContext<T>> & { _execute: () => any | Promise<any> }>
