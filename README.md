# AI Unity Debugging Tool with GPT Assistants

Welcome to **ai-debugger**! This framework allows you to create a powerful OpenAI Assistant, "the dope coder," tailored to work with your Unity project's files. At runtime, the assistant utilizes reflection and a simple UI, enabling you to ask questions about the runtime environment and project codebase using voice or text. Additionally, it provides the ability to review runtime values for any active class.

<img src="https://i.imgur.com/g6TTw2C.png" alt="Assistant Runtime" width="50%"/>

In the Unity Editor, you can traverse your filesystem and selectively upload files to the assistant's vector store, enhancing its understanding and response accuracy. The tool is designed for both VR and non-VR Unity projects, providing advanced analysis and insights into the relationships among components, their runtime behaviors, and overall code functionality.

## Features

- **Voice and Text Interaction**: Communicate with the assistant using voice or text.
- **Runtime Reflection**: Query the runtime environment and get detailed responses about your project's codebase.
- **Proximity-based scene scanning**: Scan objects with filters (e.g., colliders, distance, library/namespace).
- **Class Value Inspection**: Review runtime values for any active class.
- **Selective File Upload**: Traverse your filesystem in the Unity Editor and upload files to the assistant's vector store.
- **Simple UI**: User-friendly interface for interacting with the assistant.


## Usage
<img src="https://i.imgur.com/Ti3vPUm.png" alt="AssistantBuilderInterface" width="50%"/>

### In-Editor
1. Obtain your OpenAI API key from [OpenAI](https://platform.openai.com/account/api-keys) and configure it in the assistant setup.
2. Open the "Dope Coder" window from the Unity Editor menu by selecting **Tools > Dope Coder > GPT Assistant Builder**.
3. In the GPT Assistant Builder window, create a new assistant or load/edit an existing assistant.
   - To create a new assistant, enter the assistant name, instruction, and select the model.
   - To load an existing assistant, choose from the list and click "Load."
4. Use the file system traversal feature to select and upload files to the assistant's vector store.
5. Save and configure the assistant as needed.

### At Runtime
1. Start your Unity project.
2. Interact with the assistant through the provided UI.
3. Use voice or text input to ask questions about the runtime environment and project codebase.
4. Inspect runtime values for any active class using the reflection features.

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

## Contributing
Contributions are welcome! Please follow these steps to contribute:

1. Fork the repository.
2. Create a new branch for your feature or bugfix.
3. Implement your changes.
4. Submit a pull request with a detailed description of your changes.

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
