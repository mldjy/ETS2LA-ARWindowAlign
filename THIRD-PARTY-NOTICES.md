# Third-party notices

## Lib.Harmony 2.4.2

The release package and the local build directory (`lib/0Harmony.dll`) include
**Lib.Harmony** by Andreas Pardeike, used to apply this plugin's runtime patches.

- Project: https://github.com/pardeike/Harmony
- Package: https://www.nuget.org/packages/Lib.Harmony/2.4.2
- Licence: MIT

```
MIT License

Copyright (c) 2017 Andreas Pardeike

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ETS2LA

This project is a third-party plugin for **ETS2LA** (https://github.com/ETS2LA/ETS2LA)
and is not affiliated with or endorsed by it. No ETS2LA binaries are redistributed:
`ETS2LA.Shared.dll` and `ETS2LA.Logging.dll` are referenced at compile time only,
from the user's own ETS2LA installation, and are never bundled here.

This plugin is independently written: it uses no ETS2LA code or resources, and it does
not modify any ETS2LA program file on disk — it is loaded through ETS2LA's own plugin
mechanism and everything it does takes effect in process memory only (runtime patches
applied by Lib.Harmony).

## Euro Truck Simulator 2 / American Truck Simulator

Game names and trademarks belong to SCS Software. This project ships no game assets.
