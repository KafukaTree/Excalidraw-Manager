# Third-party notices

Excalidraw Manager is an independent project and is not affiliated with or
endorsed by Excalidraw.

The local formula editor redistributes pinned browser assets from MathLive
0.110.0 and MathJax 4.1.3. Their license files are included next to the
redistributed files under `runtime/formula-editor/vendor/`.

## MathLive 0.110.0

Source: <https://github.com/arnog/mathlive/releases/tag/v0.110.0>

License: MIT

## MathJax 4.1.3 and MathJax New Computer Modern font 4.1.3

Source: <https://github.com/mathjax/MathJax-src/releases/tag/4.1.3>

Font source: <https://github.com/mathjax/MathJax-fonts>

License: Apache-2.0

## Optional RapidLaTeXOCR 0.0.9 provider (not redistributed)

The repository includes an adapter and an explicit helper script that a user
may choose to run to install RapidLaTeXOCR and its dependencies into a separate
local Python environment. RapidLaTeXOCR, its Python dependencies, and its model
weights are **not** included in this repository or in Excalidraw Manager release
packages.

Project source: <https://github.com/RapidAI/RapidLaTeXOCR>

Python package: <https://pypi.org/project/rapid-latex-ocr/0.0.9/>

Model asset source:
<https://github.com/RapidAI/RapidLaTeXOCR/releases/tag/v0.0.0>

The RapidLaTeXOCR repository contains an MIT license file, while its Python
package metadata identifies the Apache Software License. Review the exact
upstream version and terms before installing, using, or redistributing it.

The referenced ONNX weights originate from pix2tex and are marked upstream as
**CC BY-NC-SA**. Those terms include non-commercial and share-alike conditions.
The installation script therefore requires an explicit
`-AcceptUpstreamModelLicense` acknowledgement and does not place the weights in
this project's source tree or public packages. This notice is not legal advice;
users and redistributors are responsible for confirming that their intended use
complies with the upstream terms.

The generated file `dist/runtime/main.js` is derived from the browser client
distributed with `excalidraw-edit@0.1.1`. At runtime, the manager also serves
the remaining static assets from the user's global `excalidraw-edit`
installation. Those assets incorporate Excalidraw and its dependencies.

## excalidraw-edit 0.1.1

Source: <https://github.com/wh1le/excalidraw-edit/tree/v0.1.1>

MIT License

Copyright (c) 2026 wh1le

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

## Excalidraw

Source: <https://github.com/excalidraw/excalidraw>

MIT License

Copyright (c) 2020 Excalidraw

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
