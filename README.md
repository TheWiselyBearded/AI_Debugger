# AI Unity Debugging Tool with GPT Assistants

## Description
This Unity-based debugging tool uniquely integrates with GPT Assistants, creating a dedicated assistant for each project. It allows users to upload specific files and scans codebases, leveraging the Reflection library. This combination enables the tool to utilize runtime values and pre-uploaded content to assist in debugging and elucidating the codebase at runtime. The tool is designed for both VR and non-VR Unity projects, providing advanced analysis and insights into the relationships among components, their runtime behaviors, and overall code functionality.


## Features
- Proximity-based scanning of VR objects.
- Reflection to gather detailed information about object classes.
- Integration with GPT for advanced analysis of runtime environment.
- Continuous updates and improvements.

## Usage
1. **API Key Configuration**: Initially, create an OpenAI configuration file and insert your API key.
2. **Scan Directories**: Navigate to `Tools > Generate PDF` in Unity to scan directories. This step collects information about your project's codebase.
3. **Create GPT Assistant**: Access the 'Assistants' window by going to `Tools > GPT Assistant Builder`. Here, create your GPT assistant by specifying its name and model.
4. **Attach Files**: In the 'Assistants' window, attach files that include data about your codebase, such as class structures and method descriptions.
5. **Runtime Query**: The tool queries the GPT assistant during runtime, combining the GPT responses with the scanned documentation to provide insights about your code's functionality and assist in troubleshooting.

## Installation

Follow these steps to install the necessary packages and the VR Debugging Tool plugin into your Unity project:

1. **OpenAI Unity Package**:
   - Start by installing the OpenAI Unity packages. Visit the [OpenAI Unity package repository](https://github.com/RageAgainstThePixel/com.openai.unity) for detailed instructions.

2. **Configure Package Manager**:
   - In your Unity project settings, navigate to the Package Manager settings.
   - Add the OpenUPM package registry with the following details:
     - Name: OpenUPM
     - URL: `https://package.openupm.com`
     - Scope(s):
       - `com.openai`
       - `com.utilities`

3. **Install OpenAI Package**:
   - Open the Unity Package Manager window.
   - Change the Registry from Unity to My Registries.
   - Find and add the OpenAI package.

4. **NaughtyAttributes Extension**:
   - Open your Unity Package Manager.
   - Choose "Add package from git URL".
   - Enter `https://github.com/dbrizov/NaughtyAttributes.git#upm` and click Add.
   - For more details visit the [NaughtyAttributes repository](https://github.com/dbrizov/NaughtyAttributes).

5. **Jimmy Unity Utilities Package**:
   - Still in the Unity Package Manager.
   - Choose "Add package from git URL".
   - Enter `https://github.com/JimmyCushnie/JimmysUnityUtilities.git` and click Add.
   - For more details visit the [Jimmy Unity Utilities repository](https://github.com/JimmyCushnie/JimmysUnityUtilities).
  
6. **Import VR Debugging Tool Plugin**:
   - After all necessary packages are installed, you can import this plugin via the release page.


## License
<!-- Released under the [MIT license](LICENSE). -->

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

The MIT License (MIT)

Copyright © 2024 Alireza Bahremand

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
