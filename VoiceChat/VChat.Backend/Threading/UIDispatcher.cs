using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace VChat.Backend.Threading
{
    public static class UIDispatcher
    {
        private static CoreDispatcher dispatcher;
        public static void Initialize()
        {
            dispatcher = Window.Current.Dispatcher;
        }

        public async static void BeginExecute(Action action)
        {
            if (dispatcher.HasThreadAccess)
                action();

            else
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }

        public static void Execute(Action action)
        {
            InnerExecute(action).Wait();
        }

        private static async Task InnerExecute(Action action)
        {
            if (dispatcher.HasThreadAccess)
                action();

            else await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
        }

    }
}
