using SDL2;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SDL2.SDL;
using SysDVR.Client.GUI;
using System.Runtime.InteropServices;
using SysDVR.Client.Targets.Player;
using System.Threading;
using System.Collections.Concurrent;
using BitMiracle.LibJpeg.Classic;
using System.IO;

namespace SysDVR.Client
{
    enum InputProviderEvent {
        Other = -1,
        Key,
        Button,
        Axis,
        Touch,
        Render,
    }

    class InputProviderEventHandle {
        public InputProviderEvent type;
        public SDL_Event? sdlEvent;
        public InputProviderEventHandle(InputProviderEvent type, SDL_Event? sdlEvent = null) {
            this.type = type;
            this.sdlEvent = sdlEvent;
        }
    }

    public class InputProvider : IDisposable
    {
        internal SysBotbase Bot;
        internal Dictionary<SDL_GameControllerButton, SwitchButton> joyButMappng = new();
        internal Dictionary<byte, byte> joyAxisMapping = new();
        internal DateTime AxisLastSent = DateTime.Now;
        internal short[] joyAxisValues =  {0, 0, 0, 0};
        internal Dictionary<SDL_Keycode, SwitchButton> keyMapping = new();
        internal bool updating = false;
        internal bool inTouchMode = false;
        internal bool Screen = true;
        internal DateTime mouseDownTime = DateTime.Now;
        IntPtr controller = IntPtr.Zero;
        const int deadzone = 5000;
        const int LongPressMs = 500;
        const int FailDelayMs = 100;
        const int RenderDelayMs = 1000;
        const int AxisUpdateRateMs = 500;
        CancellationToken cancel;
        LinkedList<InputProviderEventHandle> events = new();
        ConcurrentQueue<InputProviderEvent> renderEvents = new();
        DateTime RenderLastRequest = DateTime.Now;

        public InputProvider(ref SysBotbase Bot)
        {
            this.Bot = Bot;
            Bot.OnState += OnState;

            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A, SwitchButton.A);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B, SwitchButton.B);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X, SwitchButton.X);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y, SwitchButton.Y);

            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK, SwitchButton.MINUS);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START, SwitchButton.PLUS);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE, SwitchButton.HOME);

            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK, SwitchButton.LSTICK);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK, SwitchButton.RSTICK);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER, SwitchButton.L);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER, SwitchButton.R);
            
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP, SwitchButton.DUP);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN, SwitchButton.DDOWN);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT, SwitchButton.DLEFT);
            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT, SwitchButton.DRIGHT);

            joyButMappng.Add(SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MISC1, SwitchButton.CAPTURE);




            joyAxisMapping.Add(0, 0);
            joyAxisMapping.Add(1, 1);
            joyAxisMapping.Add(2, 2);
            joyAxisMapping.Add(3, 3);





            keyMapping.Add(SDL_Keycode.SDLK_UP, SwitchButton.DUP);
            keyMapping.Add(SDL_Keycode.SDLK_DOWN, SwitchButton.DDOWN);
            keyMapping.Add(SDL_Keycode.SDLK_LEFT, SwitchButton.DLEFT);
            keyMapping.Add(SDL_Keycode.SDLK_RIGHT, SwitchButton.DRIGHT);

            keyMapping.Add(SDL_Keycode.SDLK_4, SwitchButton.A);
            keyMapping.Add(SDL_Keycode.SDLK_3, SwitchButton.B);
            keyMapping.Add(SDL_Keycode.SDLK_2, SwitchButton.X);
            keyMapping.Add(SDL_Keycode.SDLK_1, SwitchButton.Y);

            keyMapping.Add(SDL_Keycode.SDLK_5, SwitchButton.LSTICK);
            keyMapping.Add(SDL_Keycode.SDLK_6, SwitchButton.RSTICK);

            keyMapping.Add(SDL_Keycode.SDLK_x, SwitchButton.L);
            keyMapping.Add(SDL_Keycode.SDLK_c, SwitchButton.R);
            keyMapping.Add(SDL_Keycode.SDLK_z, SwitchButton.ZL);
            keyMapping.Add(SDL_Keycode.SDLK_v, SwitchButton.ZR);

            keyMapping.Add(SDL_Keycode.SDLK_MINUS, SwitchButton.MINUS);
            keyMapping.Add(SDL_Keycode.SDLK_PLUS, SwitchButton.PLUS);
            keyMapping.Add(SDL_Keycode.SDLK_EQUALS, SwitchButton.PLUS);
            keyMapping.Add(SDL_Keycode.SDLK_HOME, SwitchButton.HOME);
            keyMapping.Add(SDL_Keycode.SDLK_h, SwitchButton.HOME);
            keyMapping.Add(SDL_Keycode.SDLK_END, SwitchButton.CAPTURE);
            keyMapping.Add(SDL_Keycode.SDLK_j, SwitchButton.CAPTURE);
        }

        public void Dispose() {
            cancel.Register(() => {});
            CloseController();
            for (int i = 0; i < joyAxisValues.Length; i++)
                joyAxisValues[i] = 0;
            updating = false;
            inTouchMode = false;
            Screen = true;
        }

        void Enqueue(InputProviderEventHandle inputEvent) {
            lock(events)
                events.AddFirst(inputEvent);
        }

        bool TryDequeue(out InputProviderEventHandle? inputEvent) {
            bool result = false;
            inputEvent = null;
            lock (events) {
                if (events.Count > 0) {
                    inputEvent = events.First.Value;
                    events.RemoveFirst();
                    result = true;
                }
            }
            return result;
        }

        void Clear() {
            events.Clear();
            renderEvents.Clear();
        }

        void RemoveEvents(InputProviderEvent type) {
            lock (events) {
                var node = events.First;
                while (node != null) {
                    var nextNode = node.Next;
                    if (node.Value.type == type) {
                        events.Remove(node);
                    }
                    node = nextNode;
                }
            }
        }

        public void RequestRendering() {
            if (!updating && (DateTime.Now - RenderLastRequest).TotalMilliseconds > RenderDelayMs) {
                lock (renderEvents)
                    renderEvents.Enqueue(InputProviderEvent.Render);
                RenderLastRequest = DateTime.Now;
            }
        }

        void OnState(bool state) {
            if (state) {
                RenderLastRequest = DateTime.Now;
                OpenController();
                new Thread(MainLoop).Start();
                new Thread(FallbackFrameUpdateLoop).Start();
            } else
                Dispose();
        }
        
        IntPtr FindController() {
            for (int i = 0; i < SDL_NumJoysticks(); i++)
                if (SDL_IsGameController(i) == SDL_bool.SDL_TRUE)
                    return SDL_GameControllerOpen(i);

            return IntPtr.Zero;
        }

        void OpenController(int? joystickId = null)
        {
            if (controller == IntPtr.Zero) {
                if (joystickId == null)
                    controller = FindController();
                else
                    controller = SDL_GameControllerOpen((int)joystickId);
            }
        }

        void CloseController() {
            Clear();
            if (controller != IntPtr.Zero) {
                SDL_GameControllerClose(controller);
                controller = IntPtr.Zero;
            }
        }

        void ProcessControllerButton(SDL_ControllerButtonEvent buttonEvent) {
            SwitchButton button = joyButMappng.GetValueOrDefault((SDL_GameControllerButton)buttonEvent.button);

            if (buttonEvent.type == SDL_EventType.SDL_CONTROLLERBUTTONDOWN)
                Bot.Press(button);
            if (buttonEvent.type == SDL_EventType.SDL_CONTROLLERBUTTONUP)
                Bot.Release(button);
        }

        bool ProcessKey(SDL_KeyboardEvent keyboardEvent) {
            if (keyMapping.ContainsKey(keyboardEvent.keysym.sym))
            {
                SwitchButton button = keyMapping.GetValueOrDefault(keyboardEvent.keysym.sym);

                if (keyboardEvent.type == SDL_EventType.SDL_KEYDOWN)
                    Bot.Press(button);
                if (keyboardEvent.type == SDL_EventType.SDL_KEYUP)
                    Bot.Release(button);
                return true;
            }
            return false;
        }

        bool Connected {
            get {
                return Bot?.Socket?.Connected == true;
            }
        }

        public bool HandleEvent(SDL_Event evt)
        {
            bool isController = false;
            if (Connected)
            {
                switch (evt.type) {
                    case SDL_EventType.SDL_WINDOWEVENT:
                    case SDL_EventType.SDL_TEXTEDITING:
                    case SDL_EventType.SDL_AUDIODEVICEADDED:
                    case SDL_EventType.SDL_MOUSEMOTION:
                        break;
                    case SDL_EventType.SDL_JOYDEVICEADDED:
                        OpenController(evt.cdevice.which);
                        break;
                    case SDL_EventType.SDL_JOYDEVICEREMOVED:
                        CloseController();
                        OpenController();
                        break;
                    case SDL_EventType.SDL_JOYAXISMOTION:
                        isController = true;
                        break;
                    case SDL_EventType.SDL_CONTROLLERAXISMOTION:
                        switch (evt.caxis.axis) {
                            case 4:
                                if (evt.caxis.axisValue > deadzone)
                                    Bot.Press(SwitchButton.ZL);
                                else
                                    Bot.Release(SwitchButton.ZL);
                                isController = true;
                                break;
                            case 5:
                                if (evt.caxis.axisValue > deadzone)
                                    Bot.Press(SwitchButton.ZR);
                                else
                                    Bot.Release(SwitchButton.ZR);
                                isController = true;
                                break;
                            default:
                                break;
                        }

                        if (joyAxisMapping.ContainsKey(evt.caxis.axis)) {
                            isController = true;

                            byte axis = joyAxisMapping.GetValueOrDefault(evt.caxis.axis);
                            bool isX = (axis & 0x1) == 0;
                            short value = evt.caxis.axisValue;
                            value = isX ? value : (short)(65535 - value);
                            value = value >= deadzone || value <= -deadzone ? value : (short)0;
                            joyAxisValues[axis] = value;
                            Enqueue(new(InputProviderEvent.Axis, null));
                        }
                        break;
                    case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                    case SDL_EventType.SDL_CONTROLLERBUTTONUP:
                        if (joyButMappng.ContainsKey((SDL_GameControllerButton)evt.cbutton.button)) {
                            isController = true;
                            Enqueue(new(InputProviderEvent.Button, evt));
                        }
                        break;
                    case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        mouseDownTime = DateTime.Now;
                        break;
                    case SDL_EventType.SDL_MOUSEBUTTONUP:
                        if ((DateTime.Now - mouseDownTime).TotalMilliseconds > LongPressMs)
                            inTouchMode = !inTouchMode;

                        if (inTouchMode)
                            Enqueue(new(InputProviderEvent.Touch, evt));

                        isController = inTouchMode;
                        
                        break;
                    case SDL_EventType.SDL_KEYDOWN:
                    case SDL_EventType.SDL_KEYUP:
                        if (keyMapping.ContainsKey(evt.key.keysym.sym)) {
                            isController = true;
                            Enqueue(new(InputProviderEvent.Key, evt));
                        } else {
                            if (evt.type == SDL_EventType.SDL_KEYUP) {
                                if (evt.key.keysym.sym == SDL_Keycode.SDLK_k) {
                                    isController = true;
                                    ResetController();
                                }

                                if (evt.key.keysym.sym == SDL_Keycode.SDLK_l) {
                                    isController = true;
                                    RequestRendering();
                                }
                            }
                        }

                        break;
                    case SDL_EventType.SDL_JOYBUTTONDOWN:
                    case SDL_EventType.SDL_JOYBUTTONUP:
                        isController = controller != IntPtr.Zero; // Block SDLContext.PumpEvents
                        break;
                    default:
                        break;
                }
            } else {
                switch (evt.type) {
                    case SDL_EventType.SDL_CONTROLLERAXISMOTION:
                    case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                    case SDL_EventType.SDL_CONTROLLERBUTTONUP:
                    case SDL_EventType.SDL_JOYDEVICEADDED:
                        isController = true;
                        break;
                }
            }

            return isController;
        }

        PlayerCore? Core {
            get {
                try {
                    return (Program.Instance.CurrentView as PlayerView).player;
                } catch {}
                return null;
            }
        }

        void ProcessInputProviderEvent() {
            InputProviderEventHandle inputEvent;
            if (TryDequeue(out inputEvent)) {
                switch (inputEvent.type) {
                    case InputProviderEvent.Key:
                        ProcessKey(inputEvent.sdlEvent.Value.key);
                        break;
                    case InputProviderEvent.Button:
                        ProcessControllerButton(inputEvent.sdlEvent.Value.cbutton);
                        break;
                    case InputProviderEvent.Touch:
                        SDL_Event evt = inputEvent.sdlEvent.Value;
                        double x = Math.Clamp((double)evt.motion.x / Core?.DisplayRect.w ?? 1, 0, 1);
                        double y = Math.Clamp((double)evt.motion.y / Core?.DisplayRect.h ?? 1, 0, 1);
                        Bot.Socket?.Send(SwitchExtendedCommand.Touch(x, y));
                        break;
                    case InputProviderEvent.Axis:
                        RemoveEvents(InputProviderEvent.Axis);

                        if ((DateTime.Now - AxisLastSent).TotalMilliseconds > AxisUpdateRateMs) {
                            var cmd = SwitchCommand.SetStick(SwitchStick.LEFT, joyAxisValues[0], joyAxisValues[1]);
                            Bot.Socket?.Send(cmd);
                            cmd = SwitchCommand.SetStick(SwitchStick.RIGHT, joyAxisValues[2], joyAxisValues[3]);
                            Bot.Socket?.Send(cmd);

                            AxisLastSent = DateTime.Now;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        void MainLoop() {
            while (Connected && !cancel.IsCancellationRequested) {
                ProcessInputProviderEvent();
                RequestRendering();
            }
        }

        async void FallbackFrameUpdateLoop() {
            while (Connected && !cancel.IsCancellationRequested) {
                bool containRender = false;

                lock (renderEvents) {
                    if (renderEvents.Count > 0) {
                        containRender = true;
                        renderEvents.Clear();
                    }
                }

                if (containRender) {
                    if (Core != null) {
                        if ((DateTime.Now - Core.Video.VideoLastUpdate).TotalMilliseconds > RenderDelayMs) {
                            new Thread(() => {
                                Console.Write("[SysBotbase] PixelPeeking .");
                                PixelPeek();
                                Core.Video.VideoLastUpdate = DateTime.Now;
                                Console.WriteLine("done!");
                            }).Start();
                        }
                    }
                } else
                    await Task.Delay(FailDelayMs, cancel).ConfigureAwait(false);
            }
        }

        public void ResetController() {
            CloseController();
            OpenController();
            Bot?.Socket?.Flush();
            Bot?.Socket?.Send(SwitchCommand.DetachController());
            Bot?.Socket?.Flush();
        }

        public void Click(SwitchButton button) {
            Bot?.Socket?.Send(SwitchCommand.Click(button));
        }

        public void ToggleScreen() {
            Bot?.Socket?.Send(SwitchCommand.SetScreen(Screen ? ScreenState.On : ScreenState.Off));
            Screen = !Screen;
        }

        unsafe bool PixelPeekInternal() {
            bool updated = false;
            if (Connected && !updating) {
                updating = true;
                try {
                    VideoPlayer Video = Core.Video;
                    byte[] Frame = Bot?.Socket?.PixelPeek() ?? new byte[0];

                    if (Frame != null) {
                        UpdateYUVTexture(Frame, Video);
                        updated = true;
                    }
                } catch {
                    updated = true;
                }
                updating = false;
            }
            return updated;
        }

        void PixelPeek() {
            if (!updating)
                while (!PixelPeekInternal())
                    Console.Write(".");
        }

        void UpdateYUVTexture(byte[] frame, VideoPlayer Video)
        {
            jpeg_error_mgr errorManager = new jpeg_error_mgr();
            jpeg_decompress_struct image = new jpeg_decompress_struct(errorManager);
            Stream input = new MemoryStream(frame);
            image.jpeg_stdio_src(input);
            image.jpeg_read_header(true);
            image.jpeg_start_decompress();

            int width = image.Output_width;
            int height = image.Output_height;

            byte[] yBuffer = new byte[width * height];
            byte[] uBuffer = new byte[width * height / 4];
            byte[] vBuffer = new byte[width * height / 4];

            int yIndex = 0, uvIndex = 0;
            for (int _y = 0; _y < height; _y++)
            {
                byte[][] rowBuffer = {new byte[width * 3]};
                image.jpeg_read_scanlines(rowBuffer, 1);
                var pixels = rowBuffer[0];
                for (int _x = 0; _x < width; _x++)
                {
                    byte r = pixels[_x * 3];
                    byte g = pixels[_x * 3 + 1];
                    byte b = pixels[_x * 3 + 2];

                    yBuffer[yIndex++] = (byte)Math.Clamp(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16, 0, 255);

                    if (_y % 2 == 0 && _x % 2 == 0)
                    {
                        uBuffer[uvIndex] = (byte)Math.Clamp(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128, 0, 255);
                        vBuffer[uvIndex] = (byte)Math.Clamp(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128, 0, 255);
                        uvIndex++;
                    }
                }
            }
            image.jpeg_finish_decompress();

            IntPtr yPlane = Marshal.UnsafeAddrOfPinnedArrayElement(yBuffer, 0);
            IntPtr uPlane = Marshal.UnsafeAddrOfPinnedArrayElement(uBuffer, 0);
            IntPtr vPlane = Marshal.UnsafeAddrOfPinnedArrayElement(vBuffer, 0);

            int yPitch = width;
            int uvPitch = width / 2;

            lock(Video.yuvs) {
                Video.yuvs.Enqueue(new YUVTextureContext{
                    yPlane = yPlane,
                    yPitch = yPitch,
                    uPlane = uPlane,
                    vPlane = vPlane,
                    uvPitch = uvPitch,
                });
            }
        }
    }
}
