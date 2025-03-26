using SDL2;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SDL2.SDL;
using SysDVR.Client.GUI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using SysDVR.Client.Targets.Player;
using System.Threading;

namespace SysDVR.Client
{
    public class InputProvider : IDisposable
    {
        internal SysBotbase Bot;
        internal Dictionary<SDL_GameControllerButton, SwitchButton> joyButMappng = new();
        internal Dictionary<byte, byte> joyAxisMapping = new();
        internal DateTime axisSentTime = DateTime.Now;
        internal short[] joyAxisValues =  {0, 0, 0, 0};
        internal Dictionary<SDL_Keycode, SwitchButton> keyMapping = new();
        internal bool updating = false;
        internal bool inTouchMode = false;
        internal bool Screen = true;
        internal DateTime mouseDownTime = DateTime.Now;
        IntPtr controller = IntPtr.Zero;

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
            CloseController();
            for (int i = 0; i < joyAxisValues.Length; i++)
                joyAxisValues[i] = 0;
            updating = false;
            inTouchMode = false;
            Screen = true;
        }

        void OnState(bool state) {
            if (state) {
                OpenController();
                Task.Run(FallbackFrameUpdateLoop);
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
            if (controller != IntPtr.Zero) {
                SDL_GameControllerClose(controller);
                controller = IntPtr.Zero;
            }
        }

        bool ProcessControllerButton(SDL_ControllerButtonEvent buttonEvent) {
            if (joyButMappng.ContainsKey((SDL_GameControllerButton)buttonEvent.button)) {
                SwitchButton button = joyButMappng.GetValueOrDefault((SDL_GameControllerButton)buttonEvent.button);

                if (buttonEvent.type == SDL_EventType.SDL_CONTROLLERBUTTONDOWN)
                    Bot.Press(button);
                if (buttonEvent.type == SDL_EventType.SDL_CONTROLLERBUTTONUP)
                    Bot.Release(button);
                return true;
            }
            return false;
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
                isController = true;
                switch (evt.type) {
                    case SDL_EventType.SDL_WINDOWEVENT:
                    case SDL_EventType.SDL_TEXTEDITING:
                    case SDL_EventType.SDL_AUDIODEVICEADDED:
                    case SDL_EventType.SDL_MOUSEMOTION:
                        isController = false;
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
                                Click(SwitchButton.ZL);
                                break;
                            case 5:
                                Click(SwitchButton.ZR);
                                break;
                        }
                        isController = true;
                        if (joyAxisMapping.ContainsKey(evt.caxis.axis)) {
                            byte axis = joyAxisMapping.GetValueOrDefault(evt.caxis.axis);
                            bool isX = (axis & 0x1) == 0;
                            int deadzone = 1000;
                            short value = evt.caxis.axisValue;
                            value = (short)((value < 0 ? -value : value) <= deadzone ? 0 : value);
                            value = isX ? value : (short)-value;
                            joyAxisValues[axis] = value;
                            if ((DateTime.Now - axisSentTime).TotalSeconds >= 0.2) {
                                var cmd = SwitchCommand.SetStick(SwitchStick.LEFT, joyAxisValues[0], joyAxisValues[1]);
                                Bot.Socket.Send(cmd);
                                cmd = SwitchCommand.SetStick(SwitchStick.RIGHT, joyAxisValues[2], joyAxisValues[3]);
                                Bot.Socket.Send(cmd);
                                axisSentTime = DateTime.Now;
                            }
                        }
                        break;
                    case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                    case SDL_EventType.SDL_CONTROLLERBUTTONUP:
                        isController = ProcessControllerButton(evt.cbutton);
                        break;
                    case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        mouseDownTime = DateTime.Now;
                        isController = false;
                        break;
                    case SDL_EventType.SDL_MOUSEBUTTONUP:
                        if ((DateTime.Now - mouseDownTime).TotalSeconds > 1)
                            inTouchMode = !inTouchMode;

                        if (inTouchMode) {
                            SDL_Rect rect = Core.DisplayRect;
                            double x = Math.Clamp((double)evt.motion.x / rect.w, 0, 1);
                            double y = Math.Clamp((double)evt.motion.y / rect.h, 0, 1);
                            Bot.Socket?.Send(SwitchExtendedCommand.Touch(x, y));
                        }

                        isController = inTouchMode;
                        
                        break;
                    case SDL_EventType.SDL_KEYDOWN:
                    case SDL_EventType.SDL_KEYUP:
                        if (!(isController = ProcessKey(evt.key))) {
                            if (evt.type == SDL_EventType.SDL_KEYUP) {
                                if (evt.key.keysym.sym == SDL_Keycode.SDLK_k) {
                                    isController = true;
                                    ResetController();
                                }

                                if (evt.key.keysym.sym == SDL_Keycode.SDLK_l) {
                                    isController = true;
                                    PixelPeekBackground();
                                }
                            }
                        }

                        break;
                    case SDL_EventType.SDL_JOYBUTTONDOWN:
                        if (evt.jbutton.button == 1)
                            isController = true; // Block SDLContext.PumpEvents
                            break;
                    default:
                        isController = false;
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

        PlayerCore Core {
            get {
                try {
                    return (Program.Instance.CurrentView as PlayerView).player;
                } catch {
                    return null;
                }
            }
        }

        public void FallbackFrameUpdateLoop() {
            while (Connected) {
                try {
                    if (Core == null)
                        Thread.Sleep(1000);
                    else if(Core != null && (DateTime.Now - Core.Video.VideoLastUpdate).TotalSeconds > 1) {
                        PixelPeek();
                        Core.Video.VideoLastUpdate = DateTime.Now;
                    }
                } catch {
                    Thread.Sleep(1000);
                }
            }
        }

        public void ResetController() {
            Bot?.Socket?.Send(SwitchCommand.DetachController());
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
                    var Frame = Bot.PixelPeek();

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
            if (!updating) {
                Console.Write("[SysBotbase] PixelPeeking.");
                while (!PixelPeekInternal()) {
                    Console.Write(".");
                }
                Console.WriteLine("[SysBotbase] PixelPeek success!");
            }
        }

        public void PixelPeekBackground() {
            Task.Run(PixelPeek);
        }

        void UpdateYUVTexture(Image<Rgba32> image, VideoPlayer Video)
        {
            int width = image.Width;
            int height = image.Height;

            byte[] yBuffer = new byte[width * height];
            byte[] uBuffer = new byte[width * height / 4];
            byte[] vBuffer = new byte[width * height / 4];

            int yIndex = 0, uvIndex = 0;
            for (int _y = 0; _y < height; _y++)
            {
                for (int _x = 0; _x < width; _x++)
                {
                    var pixel = image[_x, _y];
                    
                    byte r = pixel.R;
                    byte g = pixel.G;
                    byte b = pixel.B;

                    yBuffer[yIndex++] = (byte)Math.Clamp(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16, 0, 255);

                    if (_y % 2 == 0 && _x % 2 == 0)
                    {
                        uBuffer[uvIndex] = (byte)Math.Clamp(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128, 0, 255);
                        vBuffer[uvIndex] = (byte)Math.Clamp(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128, 0, 255);
                        uvIndex++;
                    }
                }
            }

            IntPtr yPlane = Marshal.UnsafeAddrOfPinnedArrayElement(yBuffer, 0);
            IntPtr uPlane = Marshal.UnsafeAddrOfPinnedArrayElement(uBuffer, 0);
            IntPtr vPlane = Marshal.UnsafeAddrOfPinnedArrayElement(vBuffer, 0);

            int yPitch = width;
            int uvPitch = width / 2;

            SDL_UpdateYUVTexture(Video.TargetTexture, ref Video.TargetTextureSize, yPlane, yPitch, uPlane, uvPitch, vPlane, uvPitch);
        }
    }
}
