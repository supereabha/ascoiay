using AOSharp.Bootstrap.IPC;
using EasyHook;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using AOSharp.Common.Unmanaged.Imports;
using System.Runtime.InteropServices;
using AOSharp.Common.GameData;
using AOSharp.Common.Helpers;
using AOSharp.Common.Unmanaged.DataTypes;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using Serilog;

namespace AOSharp.Bootstrap
{
    [Serializable]
    public class Main : IEntryPoint
    {
        private IPCServer _ipcPipe;
        private AppDomain _pluginAppDomain;
        private ManualResetEvent _connectEvent;
        private ManualResetEvent _unloadEvent;
        private static List<LocalHook> _hooks = new List<LocalHook>();
        private PluginProxy _pluginProxy;
        private ChatSocketListener _chatSocketListener;

        private string _lastChatInput;
        private IntPtr _lastChatInputWindowPtr;

        public Main(RemoteHooking.IContext inContext, String inChannelName)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("AOSharp.Bootstrapper.txt", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();

            _connectEvent = new ManualResetEvent(false);
            _unloadEvent = new ManualResetEvent(false);
            _chatSocketListener = new ChatSocketListener();

            //Setup IPC server that will be used for handling API requests and events.
            _ipcPipe = new IPCServer(inChannelName);
            _ipcPipe.OnConnected = OnIPCClientConnected;
            _ipcPipe.OnDisconnected = OnIPCClientDisconnected;
            _ipcPipe.RegisterCallback((byte)HookOpCode.LoadAssembly, typeof(LoadAssemblyMessage), OnAssembliesChanged);
            _ipcPipe.Start();
        }

        public void Run(RemoteHooking.IContext inContext, String inChannelName)
        {
            //If the GameController doesn't connect within 10 seconds we will unload the dll.
            if (!_connectEvent.WaitOne(10000))
                return;

            //Wait for the signal to unload the dll.
            _unloadEvent.WaitOne();
        }

        private void OnIPCClientConnected(IPCServer pipe)
        {
            //Notify the main thread we recieved a connection from the GameController.
            _connectEvent.Set();

            SetupHooks();
        }

        private void OnIPCClientDisconnected(IPCServer pipe)
        {
            UnhookAll();

            _pluginProxy.Teardown();

            _ipcPipe.Close();

            if (_pluginAppDomain != null)
            {
                AppDomain.Unload(_pluginAppDomain);
                _pluginAppDomain = null;
            }

            //Notify the main thread that it is time to unload the dll.
            _unloadEvent.Set();
        }

        private void OnAssembliesChanged(object pipe, IPCMessage message)
        {
            try
            {
                LoadAssemblyMessage msg = message as LoadAssemblyMessage;

                if (_pluginAppDomain != null)
                {
                    //Release existing AppDomain
                    AppDomain.Unload(_pluginAppDomain);
                    _pluginAppDomain = null;
                }

                if (!msg.Assemblies.Any())
                    return;

                AppDomainSetup setup = new AppDomainSetup()
                {
                    ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
                };

                _pluginAppDomain = AppDomain.CreateDomain("plugins", null, setup);

                Type type = typeof(PluginProxy);
                _pluginProxy = (PluginProxy)_pluginAppDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);

                _pluginProxy.LoadCore(_pluginAppDomain.BaseDirectory + "\\AOSharp.Core.dll");

                foreach (string assembly in msg.Assemblies)
                {
                    _pluginProxy.LoadPlugin(assembly);
                }
            }
            catch (Exception e)
            {
                //TODO: Send IPC message back to loader on error
                Log.Error(e.Message);
            }
        }

        private unsafe void SetupHooks()
        {
            CreateHook("N3.dll",
                        "?AddChildDynel@n3Playfield_t@@QAEXPAVn3Dynel_t@@ABVVector3_t@@ABVQuaternion_t@@@Z",
                        new N3Playfield_t.DAddChildDynel(N3Playfield_t__AddChildDynel_Hook));

            CreateHook("Gamecode.dll",
                        "?RunEngine@n3EngineClientAnarchy_t@@UAEXM@Z",
                        new N3EngineClientAnarchy_t.DRunEngine(N3EngineClientAnarchy_RunEngine_Hook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_SendInPlayMessage@n3EngineClientAnarchy_t@@QBE_NXZ",
                        new N3EngineClientAnarchy_t.DSendInPlayMessage(N3EngineClientAnarchy_SendInPlayMessage_Hook));

            CreateHook("GUI.dll",
                        "?TeleportStartedMessage@FlowControlModule_t@@CAXXZ",
                        new FlowControlModule_t.DTeleportStartedMessage(FlowControlModule_t_TeleportStarted_Hook));

            CreateHook("Gamecode.dll",
                        "?TeleportFailed@TeleportTrier_t@@QAEXXZ",
                        new TeleportTrier_t.DTeleportFailed(TeleportTrier_t_TeleportFailed_Hook));

            CreateHook("GUI.dll",
                        "?ModuleActivated@OptionPanelModule_c@@UAEX_N@Z",
                        new OptionPanelModule_c.DModuleActivated(OptionPanelModule_ModuleActivated_Hook));

            CreateHook("GUI.dll",
                        "?ViewDeleted@WindowController_c@@QAEXPAVView@@@Z",
                        new WindowController_c.DViewDeleted(WindowController_ViewDeleted_Hook));

            CreateHook("GUI.dll",
                        "?RemoveWindow@WindowController_c@@QAEXPAVWindow@@@Z",
                        new WindowController_c.DRemoveWindow(WindowController_RemoveWindow_Hook));

            CreateHook("MessageProtocol.dll",
                        "?DataBlockToMessage@@YAPAVMessage_t@@IPAX@Z",
                        new MessageProtocol.DDataBlockToMessage(DataBlockToMessage_Hook));

            CreateHook("Gamecode.dll",
                        "?PlayfieldInit@n3EngineClientAnarchy_t@@UAEXI@Z",
                        new N3EngineClientAnarchy_t.DPlayfieldInit(N3EngineClientAnarchy_PlayfieldInit_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequest@TeamViewModule_c@@AAEXABVIdentity_t@@ABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequest(TeamViewModule_SlotJoinTeamRequest_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequestFailedTooLow@TeamViewModule_c@@AAEXABVIdentity_t@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequestFailed(TeamViewModule_SlotJoinTeamRequestFailed_Hook));

            CreateHook("GUI.dll",
                        "?SlotJoinTeamRequestFailedTooHigh@TeamViewModule_c@@AAEXABVIdentity_t@@@Z",
                        new TeamViewModule_c.DSlotJoinTeamRequestFailed(TeamViewModule_SlotJoinTeamRequestFailed_Hook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_PerformSpecialAction@n3EngineClientAnarchy_t@@QAE_NABVIdentity_t@@@Z",
                        new N3EngineClientAnarchy_t.DPerformSpecialAction(N3EngineClientAnarchy_PerformSpecialAction_Hook));

            CreateHook("GUI.dll",
                        "?HandleGroupMessage@ChatGUIModule_c@@AAEXPBUGroupMessage_t@Client_c@ppj@@@Z",
                        new ChatGUIModule_t.DHandleGroupAction(HandleGroupMessageHook));

            CreateHook("Gamecode.dll",
                        "?N3Msg_CastNanoSpell@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@0@Z",
                        new N3EngineClientAnarchy_t.DCastNanoSpell(N3EngineClientAnarchy_CastNanoSpell_Hook));

            CreateHook("Connection.dll",
                        "?Send@Connection_t@@QAEHIIPBX@Z",
                        new Connection_t.DSend(Send_Hook));

            CreateHook("ws2_32.dll",
                        "recv",
                        new Ws2_32.RecvDelegate(WsRecv_Hook));

            if (ProcessChatInputPatcher.Patch(out IntPtr pProcessCommand, out IntPtr pGetCommand))
            {
                CommandInterpreter_c.ProcessChatInput = Marshal.GetDelegateForFunctionPointer<CommandInterpreter_c.DProcessChatInput>(pProcessCommand);
                CommandInterpreter_c.GetCommand = Marshal.GetDelegateForFunctionPointer<CommandInterpreter_c.DGetCommand>(pGetCommand);
                CreateHook(pProcessCommand, new CommandInterpreter_c.DProcessChatInput(ProcessChatInput_Hook));
                CreateHook(pGetCommand, new CommandInterpreter_c.DGetCommand(GetCommand_Hook));
            }
        }

        private void CreateHook(string module, string funcName, Delegate newFunc)
        {
            CreateHook(LocalHook.GetProcAddress(module, funcName), newFunc);
        }

        public void CreateHook(IntPtr origFunc, Delegate newFunc)
        {
            LocalHook hook = LocalHook.Create(origFunc, newFunc, this);
            hook.ThreadACL.SetExclusiveACL(new Int32[] { 0 });
            _hooks.Add(hook);
        }

        private void UnhookAll()
        {
            foreach (LocalHook hook in _hooks)
                hook.Dispose();
        }

        public unsafe int WsRecv_Hook(int socket, IntPtr buffer, int len, int flags)
        {
            int bytesRead = Ws2_32.recv(socket, buffer, len, flags);

            if (_pluginProxy != null && socket == ChatSocketListener.Socket)
            {
                byte[] trimmedBuffer = new byte[bytesRead];
                Marshal.Copy(buffer, trimmedBuffer, 0, bytesRead);

                List<byte[]> packets = _chatSocketListener.ProcessBuffer(trimmedBuffer);

                foreach (byte[] packet in packets)
                    _pluginProxy.ChatRecv(packet);
            }

            return bytesRead;
        }

        public unsafe byte ProcessChatInput_Hook(IntPtr pThis, IntPtr pWindow, IntPtr pCmdText)
        {
            StdString tokenized = StdString.Create();
            ChatGUIModule_t.ExpandChatTextArgs(tokenized.Pointer, pCmdText);
            _lastChatInput = tokenized.ToString();
            _lastChatInputWindowPtr = pWindow;

            return CommandInterpreter_c.ProcessChatInput(pThis, pWindow, pCmdText);
        }

        public unsafe IntPtr GetCommand_Hook(IntPtr pThis, IntPtr pCmdText, bool unk)
        {
            IntPtr result;
            if ((result = CommandInterpreter_c.GetCommand(pThis, pCmdText, unk)) == IntPtr.Zero && unk && _pluginProxy != null)
                _pluginProxy?.UnknownChatCommand(_lastChatInputWindowPtr, _lastChatInput);

            return result;
        }

        public unsafe void HandleGroupMessageHook(IntPtr pThis, IntPtr pGroupMessage)
        {
            bool cancel = false;
            
            if(_pluginProxy != null)
                cancel = _pluginProxy.HandleGroupMessage(pGroupMessage);

            if (!cancel)
                ChatGUIModule_t.HandleGroupMessage(pThis, pGroupMessage);
        }

        private int Send_Hook(IntPtr pConnection, uint unk, int len, byte[] buf)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.SentPacket(buf);
                }
            }
            catch (Exception) { }

            return Connection_t.Send(pConnection, unk, len, buf);
        }

        private IntPtr DataBlockToMessage_Hook(uint size, byte[] dataBlock)
        {
            //Let the client process the packet before we inspect it.
            IntPtr pMsg = MessageProtocol.DataBlockToMessage(size, dataBlock);

            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.DataBlockToMessage(dataBlock);
                }
            }
            catch (Exception) { }

            return pMsg;
        }

        private void WindowController_ViewDeleted_Hook(IntPtr pThis, IntPtr pView)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.ViewDeleted(pView);
                }
            }
            catch (Exception) { }

            WindowController_c.ViewDeleted(pThis, pView);
        }

        private void WindowController_RemoveWindow_Hook(IntPtr pThis, IntPtr pWindow)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.WindowDeleted(pWindow);
                }
            }
            catch (Exception) { }

            WindowController_c.RemoveWindow(pThis, pWindow);
        }


        private unsafe void TeamViewModule_SlotJoinTeamRequest_Hook(IntPtr pThis, IntPtr identity, IntPtr pName)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.JoinTeamRequest(identity, pName);
                }
            }
            catch (Exception) { }
        }

        private unsafe void TeamViewModule_SlotJoinTeamRequestFailed_Hook(IntPtr pThis, ref Identity identity)
        {
            try
            {
                IntPtr pEngine = N3Engine_t.GetInstance();

                if (pEngine == IntPtr.Zero)
                    return;

                N3EngineClientAnarchy_t.TeamJoinRequest(pEngine, ref identity, true);
            }
            catch (Exception) { }
        }

        private unsafe bool N3EngineClientAnarchy_PerformSpecialAction_Hook(IntPtr pThis, IntPtr identity)
        {
            bool specialActionResult = N3EngineClientAnarchy_t.PerformSpecialAction(pThis, ref *(Identity*)identity);

            try
            {
                if (_pluginProxy != null)
                {
                    if (specialActionResult)
                        _pluginProxy.ClientPerformedSpecialAction(identity);
                }
                    
            }
            catch (Exception) { }

            return specialActionResult;
        }

        private unsafe bool N3EngineClientAnarchy_CastNanoSpell_Hook(IntPtr pThis, ref Identity identity, ref Identity identity2)
        {
            return N3EngineClientAnarchy_t.CastNanoSpell(pThis, ref identity, ref identity2);
        }

        public void OptionPanelModule_ModuleActivated_Hook(IntPtr pThis, bool unk)
        {
            OptionPanelModule_c.ModuleActivated(pThis, unk);

            try
            {
                if (_pluginProxy != null)
                {
                    if (unk)
                        _pluginProxy.OptionPanelActivated(pThis, unk);
                }
            }
            catch (Exception) { }
        }

        public unsafe void FlowControlModule_t_TeleportStarted_Hook()
        {
            try
            {
                if (_pluginProxy != null)
                {
                    if (!*FlowControlModule_t.pIsTeleporting)
                        _pluginProxy.TeleportStarted();
                }
            }
            catch (Exception) { }

            FlowControlModule_t.TeleportStartedMessage();
        }

        public void TeleportTrier_t_TeleportFailed_Hook(IntPtr pThis)
        {
            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.TeleportFailed();
            }
            catch (Exception) { }

            TeleportTrier_t.TeleportFailed(pThis);
        }

        public bool N3EngineClientAnarchy_SendInPlayMessage_Hook(IntPtr pThis)
        {
            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.TeleportEnded();
            }
            catch (Exception) { }

            return N3EngineClientAnarchy_t.SendInPlayMessage(pThis);
        }

        public void N3EngineClientAnarchy_PlayfieldInit_Hook(IntPtr pThis, uint id)
        {
            N3EngineClientAnarchy_t.PlayfieldInit(pThis, id);

            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.PlayfieldInit(id);
            }
            catch (Exception) { }
        }

        public void N3EngineClientAnarchy_RunEngine_Hook(IntPtr pThis, float deltaTime)
        {
            try
            {
                if (_pluginProxy != null)
                {
                    _pluginProxy.RunPendingPluginInitializations();

                    _pluginProxy.EarlyUpdate(deltaTime);

                    N3EngineClientAnarchy_t.RunEngine(pThis, deltaTime);

                    _pluginProxy.Update(deltaTime);
                }
            }
            catch (Exception) { }
        }

        public void N3Playfield_t__AddChildDynel_Hook(IntPtr pThis, IntPtr pDynel, IntPtr pos, IntPtr rot)
        {
            //Let the client load the dynel before we notify the GameController of it's spawn.
            N3Playfield_t.AddChildDynel(pThis, pDynel, pos, rot);

            try
            {
                if (_pluginProxy != null)
                    _pluginProxy.DynelSpawned(pDynel);
            }
            catch (Exception) { }
        }
    }
}