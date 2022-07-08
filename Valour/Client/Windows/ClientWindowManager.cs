﻿using Valour.Api.Client;
using Valour.Api.Items.Planets;
using Valour.Api.Items.Planets.Channels;
using Valour.Api.Items.Messages;
using Valour.Client.Messages;
using Valour.Client.Components.Windows;

namespace Valour.Client.Windows
{
    /// <summary>
    /// This tracks and manages the open windows for the client
    /// </summary>
    public class ClientWindowManager
    {
        public Planet FocusedPlanet { get; private set; }

        private List<ClientWindow> OpenWindows = new();

        private List<ChatChannelWindow> OpenChatWindows = new();

        private ClientWindow SelectedWindow;

        public Func<Task> OnWindowSelect;

        public event Func<Planet, Task> OnPlanetFocused;

        public static ClientWindowManager Instance;

        public static WindowHolderComponent WindowHolder;

        public static event Func<ChatChannelWindow, Task> OnChannelWindowOpen;
        public static event Func<ChatChannelWindow, Task> OnChannelWindowClose;
        public static event Func<ChatChannelWindow, Task> OnChannelWindowUpdate;

        public ClientWindowManager()
        {
            Instance = this;
            ValourClient.HubConnection.Reconnected += OnSignalRReconnect;
        }

        public async Task SetFocusedPlanet(Planet planet)
        {
            if (planet is null)
            {
                if (FocusedPlanet is null)
                    return;
            }
            else
            {
                if (FocusedPlanet is not null)
                    if (planet.Id == FocusedPlanet.Id)
                        return;
            }

            FocusedPlanet = planet;

            // Ensure focused planet is open
            await ValourClient.OpenPlanet(planet);

            if (planet is null)
                Console.WriteLine("Set current planet to null");
            else
                Console.WriteLine($"Set focused planet to {planet.Name} ({planet.Id})");

            await OnPlanetFocused?.Invoke(planet);
        }

        public async Task OnSignalRReconnect(string data)
        {
            foreach (ChatChannelWindow window in OpenWindows)
            {
                if (window is ChatChannelWindow)
                {
                    await window.Component.SetupNewChannelAsync();
                }
            }
        }

        /// <summary>
        /// Swaps the channel a chat channel window is showing
        /// </summary>
        public async Task SwapWindowChannel(ChatChannelWindow window, PlanetChatChannel newChannel)
        {
            // Already that channel
            if (window.Channel.Id == newChannel.Id)
                return;

            Console.WriteLine("Swapping chat channel " + window.Channel.Name + " for " + newChannel.Name);

            if (!(OpenChatWindows.Any(x => x.Index != window.Index && x.Channel.Id == newChannel.Id)))
            {
                await ValourClient.CloseChannel(window.Channel);
                OpenChatWindows.Remove((ChatChannelWindow)window);

                if (newChannel.PlanetId !=  window.Channel.PlanetId)
                    await ClosePlanetIfNeeded(await window.Channel.GetPlanetAsync());
            }

            // Check for if planet should be closed

            window.Channel = newChannel;

            await ValourClient.OpenChannel(newChannel);

            // make sure the window is added to OpenChatWindows
            if (!(OpenChatWindows.Any(x => x.Index == window.Index))) {
                OpenChatWindows.Add((ChatChannelWindow)window);
            }

            if (OnChannelWindowUpdate is not null)
                await OnChannelWindowUpdate.Invoke(window);
        }

        public async Task ClosePlanetIfNeeded(Planet planet)
        {
            if (planet == null)
                return;

            if (!OpenChatWindows.Any(x => x.Channel.PlanetId == planet.Id))
            {
                await ValourClient.ClosePlanet(planet);

                if (FocusedPlanet == planet)
                    await SetFocusedPlanet(null);
            }
        }

        public async Task SetSelectedWindow(int index)
        {
            await SetSelectedWindow(OpenWindows[index]);
        }

        public async Task SetSelectedWindow(ClientWindow window)
        {
            if (window == null || (SelectedWindow == window)) return;

            SelectedWindow = window;

            Console.WriteLine($"Set active window to {window.Index}");

            if (window is ChatChannelWindow)
            {
                var chatW = window as ChatChannelWindow;
                await SetFocusedPlanet(await chatW.Channel.GetPlanetAsync());
            }

            if (OnWindowSelect != null)
            {
                Console.WriteLine($"Invoking window change event");
                await OnWindowSelect?.Invoke();
            }
        }

        public ClientWindow GetSelectedWindow()
        {
            return SelectedWindow;
        }

        public async Task AddWindow(ClientWindow window)
        {
            window.Index = OpenWindows.Count;
            OpenWindows.Add(window);

            if (window is ChatChannelWindow){
                var cWin = window as ChatChannelWindow;
                OpenChatWindows.Add(cWin);

                if (OnChannelWindowOpen != null)
                    await OnChannelWindowOpen.Invoke(cWin);
            }

            Console.WriteLine("Added window " + window.Index);

            ForceChatRefresh();

            if (WindowHolder != null)
                WindowHolder.Refresh();
        }

        public int GetWindowCount()
        {
            return OpenWindows.Count;
        }

        public ClientWindow GetWindow(int index)
        {
            if (index > OpenWindows.Count - 1 || index < 0)
            {
                return null;
            }

            return OpenWindows[index];
        }

        public List<ClientWindow> GetWindows()
        {
            return OpenWindows;
        }

        public List<ChatChannelWindow> GetChannelWindows(){
            return OpenChatWindows;
        }

        public void ClearWindows()
        {
            foreach (ClientWindow window in OpenWindows)
            {
                window.OnClosed();
            }

            OpenWindows.Clear();
            OpenChatWindows.Clear();

            WindowHolder.Refresh();
        }

        public async Task SetWindow(int index, ClientWindow window)
        {
            if (OpenWindows[index] == window)
            {
                return;
            }

            if (window is ChatChannelWindow && !OpenChatWindows.Contains((ChatChannelWindow)window))
            {
                var chatW = window as ChatChannelWindow;
                OpenChatWindows.Add(chatW);
            }

            window.Index = index;

            // Don't refresh! We're doing that ourselves
            await CloseWindow(index, false);

            OpenWindows.Insert(index, window);

            ForceChatRefresh();

            WindowHolder.Refresh();
        }
        public async Task CloseWindow(int index, bool refresh = true)
        {
            var window = OpenWindows[index];

            window.OnClosed();

            if (window is ChatChannelWindow)
            {
                var chatW = window as ChatChannelWindow;
                OpenChatWindows.Remove(chatW);
                await ClosePlanetIfNeeded(await chatW.Channel.GetPlanetAsync());

                if (OnChannelWindowClose != null)
                    await OnChannelWindowClose.Invoke(chatW);
            }

            OpenWindows.RemoveAt(index);

            int newInd = 0;

            if (OpenWindows.Count > 0)
            {
                foreach (ClientWindow w in OpenWindows)
                {
                    w.Index = newInd;
                    newInd++;
                }
            }

            if (refresh)
            {
                WindowHolder.Refresh();
            }
        }

        public void ForceChatRefresh()
        {
            foreach (ClientWindow window in OpenWindows)
            {
                ChatChannelWindow chat = window as ChatChannelWindow;

                if (chat != null && chat.Component != null && chat.Component.MessageHolder != null)
                {
                    // Force window refresh
                    chat.Component.MessageHolder.ForceRefresh();
                }
            }
        }
    }
}
