using Cmf.Connect.IoT.Driver.MTConnect.Common;
using Cmf.Connect.IoT.Driver.MTConnect.DriverObjects;
using Cmf.Connect.IoT.Driver.MTConnect.Extensions;
using MTConnect;
using MTConnect.Clients;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cmf.Connect.IoT.Driver.MTConnect
{
    public class MTConnectHandler
    {
        // Event handlers in JS side
        private Func<object, Task<object>> m_ConnectedHandler = null;

        private Func<object, Task<object>> m_DisconnectedHandler = null;

        private MTConnectHttpClient client;

        #region Constructors

        /// <summary>Create new instance of the MTConnect handler</summary>
        public MTConnectHandler()
        {
            try
            {
                string m_BaseLocation = Path.GetFullPath(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            }
            catch
            {
                Shared.Log.Error("Error creating MTConnectHandler!");
            }
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>Register a handler to receive the event occurrence</summary>
        /// <param name="inputValues">object with the following structure:
        ///   eventName (string): Name of the event to register
        ///   callback (function): Handler of the event
        /// </param>
        /// <returns>Boolean indicating result</returns>
        public async Task<object> RegisterEventHandler(dynamic inputValues)
        {
            var input = (IDictionary<string, object>)inputValues;

            var eventName = input.Get("eventName", "").ToLowerInvariant();
            var handler = input.Get<Func<object, Task<object>>>("callback", null);

            switch (eventName)
            {
                case "log": Shared.Log = new IoTLogger(handler); break;
                case "connect": m_ConnectedHandler = handler; break;
                case "disconnect": m_DisconnectedHandler = handler; break;

                default: Shared.Log.Error("Unknown event '{0}'", eventName); return (false);
            }

            Shared.Log.Info("Registered MTConnect driver event '{0}'", eventName);

            return (true);
        }

        /// <summary>Connect to the equipment</summary>
        /// <param name="inputValues">object containing all parameters. (TODO: document them)
        /// </param>
        /// <returns>Boolean indicating result</returns>
        public async Task<object> Connect(dynamic inputValues)
        {
            IDictionary<string, object> input = (IDictionary<string, object>)inputValues;

            Shared.Settings.Load(input);
            // Dump configuration for debug purposes
            Shared.Log.Debug("Communication parameters: {0}", Shared.Settings.Dump());

            // Destroy all previous connections
            this.DestroyConnection();

            #region Connect

            var address = Shared.Settings.Address ?? "127.0.0.1";
            var port = Shared.Settings.Port;
            var device = Shared.Settings.Device;

            this.client = port != -1
                ? new MTConnectHttpClient(address, port, device, DocumentFormat.XML)
                : new MTConnectHttpClient(address, device, DocumentFormat.XML);

            var isClientStarted = false;

            #region lifecycle events

            this.client.ClientStarted += (s, response) =>
            {
                isClientStarted = true;
                Shared.Log.Info("MTConnect Client has Started");
            };

            this.client.ClientStopped += (s, response) =>
            {
                Shared.Log.Warning("Client has stopped");
                this.Disconnect(null);
            };

            #endregion

            #region error handling events

            this.client.MTConnectError += (s, response) =>
            {
                Shared.Log.Warning($"MTConnect error {response.Errors.FirstOrDefault()}");
            };

            this.client.ConnectionError += (s, response) =>
            {
                Shared.Log.Warning("Client connection error");
                this.Disconnect(null);
            };

            this.client.InternalError += (s, response) =>
            {
                Shared.Log.Warning($"Internal Error: {response.Message}");
            };

            this.client.FormatError += (s, response) =>
            {
                Shared.Log.Warning($"Format Error: {response.Messages?.FirstOrDefault()}");
            };

            #endregion

            #region events

            this.client.ProbeReceived += (s, response) =>
            {
                Shared.Log.Debug($"Probe Received Event {response.Header.InstanceId}");
                Shared.EventDispatcher.TriggerEvent(new EventOccurrence(EventType.Probe, response.Header, response.Devices));
            };

            this.client.CurrentReceived += (s, response) =>
            {
                Shared.Log.Debug($"Current Received Event {response.Header.InstanceId}");
                Shared.EventDispatcher.TriggerEvent(new EventOccurrence(EventType.Current, response.Header, response.Streams, response.GetObservations()));
            };

            this.client.SampleReceived += (s, response) =>
            {
                Shared.Log.Debug($"Sample Received Event {response.Header.InstanceId}");
                Shared.EventDispatcher.TriggerEvent(new EventOccurrence(EventType.Sample, response.Header, response.Streams, response.GetObservations()));
            };

            this.client.AssetsReceived += (s, response) =>
            {
                Shared.Log.Debug($"Assets Received Event {response.Header.InstanceId}");
                Shared.EventDispatcher.TriggerEvent(new EventOccurrence(EventType.Assets, response.Header, response.Assets));
            };

            #endregion events

            Shared.Log.Debug("Starting MTConnect Client");

            try
            {
                this.client.Start();
                Utilities.WaitFor(Shared.Settings.ConnectingTimeout, "MTConnect Client didn't start", () => isClientStarted);
            }
            catch (Exception ex)
            {
                this.Disconnect(null);
            }
            #endregion


            return true;
        }

        /// <summary>Disconnect from the equipment</summary>
        /// <param name="inputValues">Not used</param>
        /// <returns>Boolean indicating result</returns>
        public async Task<object> Disconnect(dynamic inputValues)
        {
            IDictionary<string, object> input = (IDictionary<string, object>)inputValues;

            this.DestroyConnection();

            m_DisconnectedHandler?.Invoke(new { });
            return true;
        }

        /// <summary>Register event to register in the Server</summary>
        /// <param name="inputValues">object with the following structure:
        ///   eventId: Id of the event to register the listener
        ///   callback: Method that will be called when the reply is received
        /// <returns>Boolean indicating result</returns>
        public async Task<object> RegisterEvent(dynamic inputValues)
        {
            IDictionary<string, object> input = (IDictionary<string, object>)inputValues;

            string eventId = input.Get<string>("eventId", "");
            Func<object, Task<object>> callback = input.Get<Func<object, Task<object>>>("callback", null);

            if (string.IsNullOrEmpty(eventId))
            {
                throw new Exception("Unable to identify the event to register");
            }

            Shared.EventDispatcher.RegisterEventHandler(eventId, callback);

            Shared.Log.Info($"Registered event '{eventId}' with success!");

            return (true);
        }

        /// <summary>Unregister event from the Server</summary>
        /// <param name="inputValues">object with the following structure:
        ///   eventId: Id of the event to unregister
        /// <returns>Boolean indicating result</returns>
        public async Task<object> UnregisterEvent(dynamic inputValues)
        {
            IDictionary<string, object> input = (IDictionary<string, object>)inputValues;

            string eventId = input.Get<string>("eventId", "");
            if (string.IsNullOrEmpty(eventId))
            {
                throw new Exception("Unable to identify the event to register");
            }

            Shared.EventDispatcher.UnregisterEventHandler(eventId);

            Shared.Log.Info($"Unregistered event '{eventId}' with success!");
            return (true);
        }

        /// <summary>Get the values of requested tags</summary>
        /// <param name="inputValues">object with the following structure:
        ///   commandId: String with CommandId
        /// <returns>Command reply data</returns>
        public async Task<object> ExecuteCommand(dynamic inputValues)
        {
            IDictionary<string, object> input = (IDictionary<string, object>)inputValues;

            Newtonsoft.Json.Formatting format = Newtonsoft.Json.Formatting.None;
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(mtConnectCommand(input), format, settings);

            object mtConnectCommand(IDictionary<string, object> input)
            {
                CommandReceived commandReceived = new(input);

                switch (commandReceived.CommandType)
                {
                    case CommandType.Probe:
                        {
                            if (commandReceived.Device != null && Shared.Settings.Device != commandReceived.Device)
                            {
                                var probeClient = new MTConnectHttpProbeClient(this.client.Authority, commandReceived.Device)
                                {
                                    Timeout = this.client.Timeout,
                                    ContentEncodings = this.client.ContentEncodings,
                                    ContentType = this.client.ContentType
                                };
                                return probeClient.GetAsync(CancellationToken.None).Result;
                            }
                            return this.client.GetProbeAsync().Result;
                        }
                    case CommandType.Assets:
                        {
                            return this.client.GetAssetsAsync().Result;
                        }
                    case CommandType.Asset:
                        {
                            return this.client.GetAssetAsync(commandReceived.Asset).Result;
                        }
                    case CommandType.Current:
                        {
                            if (commandReceived.Device != null && Shared.Settings.Device != commandReceived.Device)
                            {
                                var currentClient = new MTConnectHttpCurrentClient(this.client.Authority, commandReceived.Device, path: commandReceived.Path, at: commandReceived.At)
                                {
                                    Timeout = this.client.Timeout,
                                    ContentEncodings = this.client.ContentEncodings,
                                    ContentType = this.client.ContentType
                                };
                                return currentClient.GetAsync(CancellationToken.None).Result;
                            }
                            return this.client.GetCurrentAsync(commandReceived.At, commandReceived.Path).Result;
                        }
                    case CommandType.Sample:
                        {
                            if (commandReceived.Device != null && Shared.Settings.Device != commandReceived.Device)
                            {
                                var sampleClient = new MTConnectHttpSampleClient(this.client.Authority, commandReceived.Device, path: commandReceived.Path, from: commandReceived.From, to: commandReceived.To, count: commandReceived.Count)
                                {
                                    Timeout = this.client.Timeout,
                                    ContentEncodings = this.client.ContentEncodings,
                                    ContentType = this.client.ContentType
                                };
                                return sampleClient.GetAsync(CancellationToken.None).Result;
                            }
                            return this.client.GetSampleAsync(from: commandReceived.From, to: commandReceived.To, count: commandReceived.Count, path: commandReceived.Path).Result;
                        }
                    default: throw new Exception("Unknown Command Type");
                }
            }
        }

        #endregion Public Methods

        #region Private & Internal Methods

        private void DestroyConnection()
        {
            if (this.client != null)
            {
                Shared.Log.Info("Destroying (possible) previously connection and unsubscribing all events...");

                try
                {
                    Shared.EventDispatcher.DestroyEventHandlers();
                    this.client.Stop();
                }
                catch (Exception e)
                {
                    Shared.Log.Error($"Exception while disconnecting previous MTConnect client: {e.Message}");
                }
            }
        }
        #endregion Private & Internal Methods
    }
}
