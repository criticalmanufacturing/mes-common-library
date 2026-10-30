using Cmf.Connect.IoT.Driver.MTConnect;
using Cmf.Connect.IoT.Driver.MTConnect.DriverObjects;
using System.Dynamic;

namespace ConsoleTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Start Console Test for MTConnect");

            new Test().Start();

            Console.WriteLine("Stop Console Test for MTConnect");
        }
    }

    internal class Test
    {
        private MTConnectHandler m_Handler;
        private bool m_IsDisconnected = true;

        public async void Start()
        {
            m_Handler = new MTConnectHandler();

            this.RegisterEventHandler("log", this.OnLog);
            this.RegisterEventHandler("connect", this.OnConnected);
            this.RegisterEventHandler("disconnect", this.OnDisconnected);

            await this.Connect(
                "localhost",
                5000);

            var t = await m_Handler.ExecuteCommand(new Dictionary<string, object>() {
                    {  "commandId", "abc" },
                    {  "commandParameters", new Dictionary<string, object>(){
                        { "__internalType__", "Probe" }
                    } }
                });
            await this.RegisterEventNotification(EventType.Probe, this.OnData);

            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);
            Thread.Sleep(10000);

        }

        private Task<object> OnData(dynamic inputValues)
        {
            Console.WriteLine("{0}: {1}", DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss.fff"), inputValues);

            return (null);
        }

        #region Simulate the JS calls

        private async void RegisterEventHandler(string eventName, Func<object, Task<object>> handler)
        {
            await m_Handler.RegisterEventHandler(new Dictionary<string, object>() {
                {  "eventName", eventName },
                {  "callback", handler }
            });
        }

        private async Task<object> Connect(string address, int port, string? device = null)
        {
            return (await m_Handler.Connect(new Dictionary<string, object>() {
                {  "address", address },
                {  "port", port },
                {  "device", device },
            }));
        }

        private async Task<object> RegisterEventNotification(EventType eventType, Func<object, Task<object>> handler)
        {
            dynamic eventData = new ExpandoObject();

            await m_Handler.RegisterEvent(new Dictionary<string, object>() {
                {  "eventId", eventType.ToString() },
                {  "eventData", eventData },
                {  "callback", handler }
            });

            return (null);
        }

        #endregion

        private async Task<object> OnLog(dynamic inputValues)
        {
            string verbosity = GetValue(inputValues, "verbosity", "info");
            string message = GetValue(inputValues, "message", "");

            Console.WriteLine("{0}: {1}", verbosity, message);
            return null;
        }
        private async Task<object> OnConnected(dynamic inputValues)
        {
            Console.WriteLine("*** Connected!");
            m_IsDisconnected = false;
            return null;
        }
        private async Task<object> OnDisconnected(dynamic inputValues)
        {
            Console.WriteLine("*** Disconnected!");
            m_IsDisconnected = true;
            return null;
        }

        public static T GetValue<T>(object target, string name, T defaultValue)
        {
            var site = System.Runtime.CompilerServices.CallSite<Func<System.Runtime.CompilerServices.CallSite, object, object>>.Create(Microsoft.CSharp.RuntimeBinder.Binder.GetMember(0, name, target.GetType(), new[] { Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(0, null) }));
            var value = site.Target(site, target);

            // Check nullable types and null value
            bool bCanBeNull = typeof(T).IsGenericParameter && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>);
            if (value == null && (typeof(T) == typeof(string) || bCanBeNull))
                return (default(T));

            if (value != null)
            {

                Type t = typeof(T);
                Type u = Nullable.GetUnderlyingType(t);

                try
                {
                    if (u != null)
                    {
                        return (T)Convert.ChangeType(value, u);
                    }
                    else
                    {
                        return (T)Convert.ChangeType(value, t);
                    }
                }
                catch
                {
                    // Different types, return the default value
                }
            }

            return (defaultValue);
        }
    }
}