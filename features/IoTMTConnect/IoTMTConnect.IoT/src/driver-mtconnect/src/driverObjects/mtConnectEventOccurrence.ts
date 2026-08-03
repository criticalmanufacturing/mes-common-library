import * as moment from "moment";

/** Representation of the MTConnect event occurrence */
export interface MTConnectEventOccurrence {
    /** Unique Id of the message */
    messageId: string;
    /* Source of the event (sender) */
    sourceId: string;
    /** Id of the event registered (deviceId) */
    eventId: string;
    /** Timestamp of the occurrence */
    occurrenceTimeStamp: string;
    /** Message body in json format */
    values: any;
}

/** Representation of the MTConnect Event in a more usable state for the controller */
export interface MTConnectEventOccurrenceToController {
    /** Unique Id of the message */
    messageId: string;
    /* Source of the event (sender) */
    sourceId: string;
    /** Id of the event registered (deviceId) */
    eventId: string;
    /** Timestamp of the occurrence */
    occurrenceTimeStamp: moment.Moment;
    /** Timestamp of the occurrence */
    emitTimeStamp: moment.Moment;
    /** Message body in json format */
    values: any;
}

/**
 * Convert an MTConnectEventOccurrence structure into an MTConnectEventOccurrenceToController structure
 * @param occurrence MTConnectEventOccurrence structure
 */
export function convertEventOccurrenceToController(occurrence: MTConnectEventOccurrence): MTConnectEventOccurrenceToController {
    if (occurrence == null) {
        throw new Error("MTConnect event occurrence cannot be null");
    }

    const result: MTConnectEventOccurrenceToController = {
        messageId: occurrence.messageId,
        sourceId: occurrence.sourceId,
        eventId: occurrence.eventId,
        occurrenceTimeStamp: moment(occurrence.occurrenceTimeStamp),
        emitTimeStamp: moment(),
        values: {} // Will be filled in the driverImplementation
    };

    return (result);
}
