export interface ISA95 {
    Material?: string;
    Resource?: string;
    Area?: string;
    Site?: string;
    Facility?: string;
    Enterprise?: string;
}

export interface PostTelemetryParameter {
    Class: string;
    Name: string;
    UnitOfMeasure?: string;
    NumericValues?: number[];
    StringValues?: string[];
    Timestamps: string[];
}

export interface PostTelemetryTag {
    Key: string;
    Value: string;
}

export interface PostTelemetry {
    Parameters?: PostTelemetryParameter[];
    Tags?: PostTelemetryTag[];
    Material?: { Name: string };
    Resource?: { Name: string };
    Area?: { Name: string };
    Facility?: { Name: string };
    Site?: { Name: string };
    Enterprise?: { Name: string };
}

/** A single telemetry event queued for batched posting, preserving its original occurrence time */
export interface BatchedTelemetryEvent {
    data: PostTelemetry;
    eventTime: string;
}
