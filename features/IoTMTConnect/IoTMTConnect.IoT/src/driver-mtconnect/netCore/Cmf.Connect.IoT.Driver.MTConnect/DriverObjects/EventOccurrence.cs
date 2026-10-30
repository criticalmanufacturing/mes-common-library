using MTConnect.Observations;
using System;
using System.Collections.Generic;

namespace Cmf.Connect.IoT.Driver.MTConnect.DriverObjects
{
    public class EventOccurrence
    {
        /// <summary>Unique Id of a message</summary>
        public string MessageId { get; } = Guid.NewGuid().ToString("N");
        public object Header { get; private set; }
        public IEnumerable<object> Devices { get; private set; }
        public IEnumerable<IObservation> Observations { get; private set; }
        /// <summary>Message Name of the event occurrence</summary>
        public EventType EventId { get; private set; }
        /// <summary>Timestamp of the occurrence of the event</summary>
        public DateTime OccurrenceTimeStamp { get; set; }

        public EventOccurrence(EventType eventId, dynamic header, dynamic body, dynamic observations = null)
        {
            this.EventId = eventId;
            this.Header = header;
            this.Devices = body;
            this.Observations = observations;
            this.OccurrenceTimeStamp = DateTime.UtcNow;
        }

        public dynamic ToJson()
        {
            Newtonsoft.Json.Formatting format = Newtonsoft.Json.Formatting.None;
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None;
            settings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;

            return (new
            {
                messageId = this.MessageId,
                eventId = this.EventId.ToString(),
                values = new
                {
                    // I am doing this, so as to let the path of the property handle where to pick
                    values = new
                    {
                        header = Newtonsoft.Json.JsonConvert.SerializeObject(this.Header, format, settings),
                        devices = Newtonsoft.Json.JsonConvert.SerializeObject(this.Devices, format, settings),
                        observations = Newtonsoft.Json.JsonConvert.SerializeObject(this.Observations, format, settings),
                    }
                },
                occurrenceTimeStamp = this.OccurrenceTimeStamp,
            });
        }
    }
}
