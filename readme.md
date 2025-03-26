# SysDVR
[![Discord](https://img.shields.io/discord/643436008452521984.svg?logo=discord&logoColor=white&label=Discord&color=7289DA
)](https://discord.gg/rqU5Tf8)
[![Latest release](https://img.shields.io/github/v/release/exelix11/SysDVR)](https://github.com/exelix11/SysDVR/releases)
[![Downloads](https://img.shields.io/github/downloads/exelix11/SysDVR/total)](https://github.com/exelix11/SysDVR/releases)
[![ko-fi](https://img.shields.io/badge/supporting-ko--fi-f96854)](https://ko-fi.com/exelix11)

This is a sysmodule that allows capturing the running game output to a pc via USB or network connection.

<p align="center">
  <img src="https://raw.githubusercontent.com/exelix11/SysDVR/master/.github/images/Screenshot.jpg" width="50%">
</p>

# Features
- Cross platform, can stream to Windows, Mac, Linux and Android.
- Stream via USB or Wifi.
- **Video quality is fixed to 720p @ 30fps with h264 compression, this is a hardware limit**.
- Audio quality is fixed to 16bit PCM @ 48kHz stereo. Not compressed.
- Very low latency with an optimal setup, most games are playable !

# Limitations
- **Only works on games that have video recording enabled** (aka you can long-press the capture button to save a video)
   - [There is now a workaround to support most games](https://github.com/exelix11/dvr-patches/), as it may cause issues it's hosted on a different repo and must be installed manually.
- Only captures game output. System UI, home menu and homebrews running as applet won't be captured.
- Stream quality depends heavily on the environment, bad usb wires or low wifi signal can affect it significantly.
- **USB streaming is not available when docked**
- Requires at least firmware 6.0.0

Clearly with these limitations **this sysmodule doesn't fully replace a capture card**.

# Usage
The guide has been moved to the wiki, you can find it [here](https://github.com/exelix11/SysDVR/wiki)

**If you have issues make sure to read the the [common issues page](https://github.com/exelix11/SysDVR/wiki/Troubleshooting). If you need help you can either ask on discord or open an issue with the correct template.**

## Donations
If you like my work and wish to support me you can donate on [ko-fi](https://ko-fi.com/exelix11)

## Credits
- Everyone from libnx and the people who reversed grc:d and wrote the service wrapper, mission2000 in particular for the suggestion on how to fix audio lag.
- [mtp-server-nx](https://github.com/retronx-team/mtp-server-nx) for their usb implementation
- [RTSPSharp](https://github.com/ngraziano/SharpRTSP) for the C# RTSP library
- Bonta on discord for a lot of help implementing a custom RTSP server
- [Xerpi](https://github.com/xerpi) for a lot of help while working on the UVC branch


## Forked Features
After all, this is a forked version of SysDVR from exelix11.
It mainly implemented simple keyboard/controller events and send those from SDL2 to switch using [sys-botbase](https://github.com/olliz0r/sys-botbase) and its [client codebase](https://github.com/kwsch/SysBot.NET).

It also featured with **fallback stream** (~1 FPS) using [Pixel peeking command](https://github.com/olliz0r/sys-botbase/blob/master/commands.md#screen-control) from sys-botbase. In other words, if you accidentally press something like Home key and exit the game. It will automatically fallback to low FPS stream. And you can hop back to the game.

### Key mappings
To maintain original key functionalities like screenshot, I avoid using WASD keys.

Since I only using **keyboard for testing**, it might changed significantly in the future or have a binding changing menu or not. So not recommended.
- Keyboard:
  - Arrow keys: Left joycon arrow keys
  - Number 1: Y
  - Number 2: X
  - Number 3: B
  - Number 4: A
  - Number 5: Left Stick
  - Number 6: Right Stick
  - X: L
  - C: R
  - Z: ZL
  - Y: ZR
  - -: Minus
  - +: Plus
  - Home/H: Home
  - End/J: Capture

  - Additional keyboard hotkeys:
    - K: Try fixing controller if not able to control the switch
    - L: Manually capture current screen no matter in game or not



- Controller: All keys mapped but only tested with my `IINE Switch Pro Controller` like [this one](https://www.amazon.com/Wireless-Controller-Nintendo-Switch-Support-Control/dp/B0888DJGGD)
  - No Gyro/Rumble, only events supported by [SDL2-CS](https://github.com/ppy/SDL2-CS)

## Tested and working
- Windows

### Known issues and limitations
- Android will ran into performance issues and crash.
- Joystick update rate is set to ~0.2 seconds, because high update rate will jamming the socket channel in sys-botbase and all other buttons start to **lag bad**.